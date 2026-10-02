/***************************************************************************
 *   Copyright (C) 2006 by Andi8104                                        *
 *   Andi8104@arcor.de                                                     *
 *                                                                         *
 *   Additional programming:                                               *
 *   Copyright (C) 2007-2010 by Mootilda                                   *
 *   http://Mootilda.ModTheSims.info                                       *
 *                                                                         *
 *   This program is free software; you can redistribute it and/or modify  *
 *   it under the terms of the GNU General Public License as published by  *
 *   the Free Software Foundation; either version 2 of the License, or     *
 *   (at your option) any later version.                                   *
 *                                                                         *
 *   This program is distributed in the hope that it will be useful,       *
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of        *
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the         *
 *   GNU General Public License for more details.                          *
 *                                                                         *
 *   You should have received a copy of the GNU General Public License     *
 *   along with this program; if not, write to the                         *
 *   Free Software Foundation, Inc.,                                       *
 *   59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.             *
 ***************************************************************************/

using System;
using System.Diagnostics;
using System.IO;
using SimPe.Interfaces.Files;

// To view properties for a tile, hold down <alt> and <shift> over tile.
namespace LotExpander
{
    public class R_3ARY
    {
        private IPackedFileDescriptor PFD;
        private byte[] Data;
        private const int iLeadingZeros = 64;
        private int iWidthIndex = 81;
        private int iWidth = 0;
        private int iHeightIndex = 85;
        private int iHeight = 0;
        private int iDepthIndex = 89;
        private int iDepth = 0;
        private int iHeaderSize = 93;
        private const int iLotTilesPerNeighborhoodTile = 10;
        private int iBytesPerObject = 8;
        private byte bDefaultValue = 0;

        enum DataType { dtUnknown, dt4Bytes, dt4Shorts, dt4DWORDs };
        private DataType dt = DataType.dtUnknown;

        public R_3ARY(IPackageFile LotPackage, IPackedFileDescriptor Descriptor)
        {
            PFD = Descriptor;
            IPackedFile PF = LotPackage.Read(PFD);
            Data = PF.UncompressedData;

            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            for (int i = 0; i < iLeadingZeros; i++)
            {
                byte bDummy = BR.ReadByte();
                Debug.Assert(bDummy == 0);
            }

            uint uBlockID = BR.ReadUInt32();
            if (uBlockID != 0x2A51171B)
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 3ARY: Block ID");

            int iBlockVersion = BR.ReadInt32();
            Debug.Assert(iBlockVersion == 1);   // ToDo: Determine whether other versions are known and handled correctly

            byte bBlockNameLen = (byte)BR.PeekChar();
            if ((bBlockNameLen & 0x80) != 0)
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 3ARY: Block Name Length");
            string sBlockName = BR.ReadString();
            if (sBlockName != "c3DArray")
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 3ARY: Block Name");
            int iIndex = iLeadingZeros + 4 /* uBlockID */ + 4 /* iBlockVersion */ + 1 /* bBlockNameLen */ + bBlockNameLen;

            Debug.Assert(iWidthIndex == iIndex);
            iWidthIndex = iIndex;
            iWidth = BR.ReadInt32();
            iIndex += 4;

            Debug.Assert(iHeightIndex == iIndex);
            iHeightIndex = iIndex;
            iHeight = BR.ReadInt32();
            iIndex += 4;

            Debug.Assert(iDepthIndex == iIndex);
            iDepthIndex = iIndex;
            iDepth = BR.ReadInt32();
            iIndex += 4;

            Debug.Assert(iHeaderSize == iIndex);
            iHeaderSize = iIndex;

            uint uInst = PFD.Instance;
            switch (uInst)
            {
                case 0x00000000:    // Floor tiles
                case 0x00000009:    // Place (Room ID)
                case 0x0000000A:    // Load  (Room ID)
                case 0x0000000B:    // Light (Room ID)
                    {
                        // ToDo: This is likely 4 shorts, rather than 8 bytes
                        //       Contains Room IDs from WGRA Array B
                        //       Logic only works because of 0 default value.
                        iBytesPerObject = 8;
                        bDefaultValue = 0;      // Room ID 0 is outside
                        dt = DataType.dt4Shorts;
                        break;
                    }
                case 0x00000003:    // Tile locking
                    {
                        iBytesPerObject = 1;
                        bDefaultValue = 0;
                        break;
                    }
                case 0x0000000C:    // Object instances
                    {
                        iBytesPerObject = 4;
                        bDefaultValue = 0;
                        break;
                    }
                case 0x00000014:    // Occupancy data?
                    {
                        // ToDo: This is likely 4 DWORDs, rather than 16 bytes
                        //       Logic only works because of -1 (0xFF) default value.
                        iBytesPerObject = 16;
                        bDefaultValue = 255;
                        dt = DataType.dt4DWORDs;
                        break;
                    }
                case 0x00000015:    // Visibility?
                    {
                        iBytesPerObject = 4;
                        bDefaultValue = 1;  // 1 is Visible
                        dt = DataType.dt4Bytes;
                        break;
                    }
                case 0x0000001B:
                    {
                        // ToDo: This is likely 4 WORDs or 2 DWORDs, rather than 16 bytes
                        //       Logic only works because of -1 (0xFF) default value.
                        iBytesPerObject = 8;
                        bDefaultValue = 255;
                        break;
                    }
                case 0x00000020:    // Floor tile rotation
                    {
                        iBytesPerObject = 2;
                        bDefaultValue = 0x11;   // Valid values are 0x11, 0x22, 0x44, 0x88
                        break;
                    }
                case 0x00005d00:
                    {
                        iBytesPerObject = 2;
                        bDefaultValue = 0;      // ToDo: Determine which default to use...
                        break;
                    }
                case 0x00005d01:    // Road (0007 0000 0007 0007 0007 0007 0007 0007 0000 0007, otherwise 0)
                    {
                        // ToDo: This is actually 4 shorts, rather than 8 bytes
                        //       Logic works because of 0 default value.
                        iBytesPerObject = 8;
                        bDefaultValue = 0;
                        break;
                    }
                case 0x00005d02:
                    {
                        iBytesPerObject = 2;
                        bDefaultValue = 11; // ToDo: Determine which default to use...
                        break;
                    }
                default:
                    {
                        Debug.Fail("Unknown 3D Array Instance - may not be handled correctly");
                        iBytesPerObject = 8;
                        bDefaultValue = 0;
                        break;
                    }
            }

            if (uInst == 0x00005d00)
            {
                if (iWidth != 1)
                    if (LETools.ErrorChecking)
                        throw new InvalidDataException("Invalid 3ARY Instance 5D00: Width");
                if (iHeight != 1)
                    if (LETools.ErrorChecking)
                        throw new InvalidDataException("Invalid 3ARY Instance 5D00: Height");
            }
            else
            {
                if ((Width < 1) || (Width > LETools.MaxLotSize))
                    if (LETools.ErrorChecking)
                        throw new InvalidDataException("Invalid 3ARY: Width");
                if ((Height < 1) || (Height > LETools.MaxLotSize))
                    if (LETools.ErrorChecking)
                        throw new InvalidDataException("Invalid 3ARY: Height");
            }
/*          // ToDo: What are valid values for Depth?
 *          // 30 Middle Lane (empty movable lot) == 0!
 *          if (Depth < 1)
 *              if (LETools.ErrorChecking)
 *                  throw new InvalidDataException("Invalid 3ARY: Depth");
 */
 
            if (Data.Length != ExpectedLength)
            {
                Debug.Fail("3D Array has unexpected length");
                // If we are here, then probably dealing with a new IPFD.Instance added by a new expansion pack...
                // For a correct solution, we need to determine the structure of the new instance.
                // However, as an interim solution, let's assume that iBytesPerObject is incorrect.
                iBytesPerObject = (Data.Length - iHeaderSize) / (iWidth * iHeight * iDepth);
                Debug.Assert(Data.Length == ExpectedLength);
                if (Data.Length != ExpectedLength)
                    if (LETools.ErrorChecking)
                        throw new InvalidDataException("Invalid 3ARY: Data Length");
            }

        }

        private int ExpectedLength
        {
            get
            {
                return iWidth * iHeight * iDepth * iBytesPerObject + iHeaderSize;
            }
        }

        private void ReplaceInt(int i, int iIndex)
        {
            byte[] BA = new byte[4];
            BinaryWriter BW = new BinaryWriter(new MemoryStream(BA));
            BW.Write(i);
            Array.Copy(BA, 0, Data, iIndex, 4);
            PFD.SetUserData(Data, true);

            Debug.Assert(Data.Length == ExpectedLength);
        }

#if ADJUST
        private void ReplaceWidth(int i)
        {
            iWidth = i;
            ReplaceInt(iWidth, iWidthIndex);
        }

        private void ReplaceHeight(int i)
        {
            iHeight = i;
            ReplaceInt(iHeight, iHeightIndex);
        }
#endif

#if ! ADJUST
        private void ReplaceDepth(int i)
        {
            iDepth = i;
            ReplaceInt(iDepth, iDepthIndex);
        }
#endif

        // First, add width to end of array:
        //
        // |-------------------|
        // | Original  | Width |
        // |-------------------|
        //
        //
        // Second, add height to end of array:
        // 
        // |-------------------|
        // | Original  | Width |
        // |-------------------|
        // |      Height       |
        // |-------------------|
        //
        //
        // Third, add width to start of array:
        //
        // |---------------------------|
        // | Width | Original  | Width |
        // |  Rev  |-------------------|
        // |       |      Height       |
        // |---------------------------|
        //
        // Finally, add height to start of array:
        // 
        // |---------------------------|
        // |         HeightRev         |
        // |---------------------------|
        // | Width | Original  | Width |
        // |  Rev  |-------------------|
        // |       |      Height       |
        // |---------------------------|

        public int Width
        {
            get
            {
                return iWidth / iLotTilesPerNeighborhoodTile;
            }
#if ADJUST
            set
            {
                int iWidthDiff = (value - this.Width);
                if (iWidthDiff > 0)
                {
                    byte[] DataNew = new byte[Data.Length + iWidthDiff * iHeight * iDepth * iBytesPerObject];
                    //Am Ende einfügen
                    Array.Copy(Data, DataNew, iHeaderSize);
                    int iOldIndex = iHeaderSize;
                    int iNewIndex = iHeaderSize;
                    int iCopySize = iWidth * iHeight * iBytesPerObject;
                    for (int i = 0; i < iDepth; i++)
                    {
                        Array.Copy(Data, iOldIndex, DataNew, iNewIndex, iCopySize);
                        iOldIndex += iCopySize;
                        iNewIndex += iCopySize;
                        for (int j = 0; j < iWidthDiff * iHeight * iBytesPerObject; j++)
                        {
                            DataNew[iNewIndex++] = bDefaultValue;
                        }
                    }
                    Data = DataNew;
                    ReplaceWidth(value);
                }
                else if (iWidthDiff < 0)
                {
                    byte[] DataNew = new byte[Data.Length + iWidthDiff * iHeight * iDepth * iBytesPerObject];
                    Array.Copy(Data, DataNew, iHeaderSize);
                    int iOldIndex = iHeaderSize;
                    int iNewIndex = iHeaderSize;
                    int iCopySize = (iWidth + iWidthDiff) * iHeight * iBytesPerObject;
                    for (int i = 0; i < iDepth; i++)
                    {
                        Array.Copy(Data, iOldIndex, DataNew, iNewIndex, iCopySize);
                        iOldIndex += iCopySize + (0 - iWidthDiff) * iHeight * iBytesPerObject;
                        iNewIndex += iCopySize;
                    }
                    Data = DataNew;
                    ReplaceWidth(value);
                }
            }
#endif
        }

#if ADJUST
        public int WidthRev
        {
            set
            {
                int iWidthDiff = (value - this.Width);
                if (iWidthDiff > 0)
                {
                    byte[] DataNew = new byte[Data.Length + iWidthDiff * iHeight * iDepth * iBytesPerObject];
                    //Am Anfang einfügen
                    Array.Copy(Data, DataNew, iHeaderSize);
                    int iOldIndex = iHeaderSize;
                    int iNewIndex = iHeaderSize;
                    int iCopySize = iWidth * iHeight * iBytesPerObject;
                    for (int i = 0; i < iDepth; i++)
                    {
                        for (int j = 0; j < iWidthDiff * iHeight * iBytesPerObject; j++)
                        {
                            DataNew[iNewIndex++] = bDefaultValue;
                        }
                        Array.Copy(Data, iOldIndex, DataNew, iNewIndex, iCopySize);
                        iOldIndex += iCopySize;
                        iNewIndex += iCopySize;
                    }
                    Data = DataNew;
                    ReplaceWidth(value);
                }
                else if (iWidthDiff < 0)
                {
                    byte[] DataNew = new byte[Data.Length + iWidthDiff * iHeight * iDepth * iBytesPerObject];
                    Array.Copy(Data, DataNew, iHeaderSize);
                    int iOldIndex = iHeaderSize;
                    int iNewIndex = iHeaderSize;
                    int iCopySize = (iWidth + iWidthDiff) * iHeight * iBytesPerObject;
                    for (int i = 0; i < iDepth; i++)
                    {
                        iOldIndex += (0 - iWidthDiff) * iHeight * iBytesPerObject;
                        Array.Copy(Data, iOldIndex, DataNew, iNewIndex, iCopySize);
                        iOldIndex += iCopySize;
                        iNewIndex += iCopySize;
                    }
                    Data = DataNew;
                    ReplaceWidth(value);
                }
            }
        }
#endif

        public int Height
        {
            get
            {
                return iHeight / iLotTilesPerNeighborhoodTile;
            }
#if ADJUST
            set
            {
                int iHeightDiff = (value - this.Height);
                if (iHeightDiff > 0)
                {
                    byte[] DataNew = new byte[Data.Length + iWidth * iHeightDiff * iDepth * iBytesPerObject];
                    //Spaltenweise am Ende einfügen
                    Array.Copy(Data, DataNew, iHeaderSize);
                    int iOldIndex = iHeaderSize;
                    int iNewIndex = iHeaderSize;
                    int iCopySize = iHeight * iBytesPerObject;
                    for (int i = 0; i < iDepth; i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            Array.Copy(Data, iOldIndex, DataNew, iNewIndex, iCopySize);
                            iOldIndex += iCopySize;
                            iNewIndex += iCopySize;
                            for (int k = 0; k < iHeightDiff * iBytesPerObject; k++)
                            {
                                DataNew[iNewIndex++] = bDefaultValue;
                            }
                        }
                    }
                    Data = DataNew;
                    ReplaceHeight(value);
                }
                else if (iHeightDiff < 0)
                {
                    byte[] DataNew = new byte[Data.Length + iWidth * iHeightDiff * iDepth * iBytesPerObject];
                    Array.Copy(Data, DataNew, iHeaderSize);
                    int iOldIndex = iHeaderSize;
                    int iNewIndex = iHeaderSize;
                    int iCopySize = (iHeight + iHeightDiff) * iBytesPerObject;
                    for (int i = 0; i < iDepth; i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            Array.Copy(Data, iOldIndex, DataNew, iNewIndex, iCopySize);
                            iOldIndex += iCopySize + (0 - iHeightDiff) * iBytesPerObject;
                            iNewIndex += iCopySize;
                        }
                    }
                    Data = DataNew;
                    ReplaceHeight(value);
                }
            }
#endif
        }

#if ADJUST
        public int HeightRev
        {
            set
            {
                int iHeightDiff = (value - this.Height);
                if (iHeightDiff > 0)
                {
                    byte[] DataNew = new byte[Data.Length + iWidth * iHeightDiff * iDepth * iBytesPerObject];
                    //Spaltenweise am Anfang einfügen
                    Array.Copy(Data, DataNew, iHeaderSize);
                    int iOldIndex = iHeaderSize;
                    int iNewIndex = iHeaderSize;
                    int iCopySize = iHeight * iBytesPerObject;
                    for (int i = 0; i < iDepth; i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            for (int k = 0; k < iHeightDiff * iBytesPerObject; k++)
                            {
                                DataNew[iNewIndex++] = bDefaultValue;
                            }
                            Array.Copy(Data, iOldIndex, DataNew, iNewIndex, iCopySize);
                            iOldIndex += iCopySize;
                            iNewIndex += iCopySize;
                        }
                    }
                    Data = DataNew;
                    ReplaceHeight(value);
                }
                else if (iHeightDiff < 0)
                {
                    byte[] DataNew = new byte[Data.Length + iWidth * iHeightDiff * iDepth * iBytesPerObject];
                    Array.Copy(Data, DataNew, iHeaderSize);
                    int iOldIndex = iHeaderSize;
                    int iNewIndex = iHeaderSize;
                    int iCopySize = (iHeight + iHeightDiff) * iBytesPerObject;
                    for (int i = 0; i < iDepth; i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            iOldIndex += (0 - iHeightDiff) * iBytesPerObject;
                            Array.Copy(Data, iOldIndex, DataNew, iNewIndex, iCopySize);
                            iOldIndex += iCopySize;
                            iNewIndex += iCopySize;
                        }
                    }
                    Data = DataNew;
                    ReplaceHeight(value);
                }
            }
        }
#endif

        public int Depth
        {
            get
            {
                return iDepth;
            }
        }

#if ! ADJUST
        public void AddLevel(int iNewLevel, int iMinLevel)
        {
            int iLevel = iNewLevel - iMinLevel;
            if (iLevel > iDepth)
                iLevel = iDepth;

            // ToDo: must keep road for instance 0x00005d01 on ground level
            // => need to know where road goes
            int iCopySize = iWidth * iHeight * iBytesPerObject;
            byte[] DataNew = new byte[Data.Length + iCopySize];
            Array.Copy(Data, DataNew, iHeaderSize);
            int iOldIndex = iHeaderSize;
            int iNewIndex = iHeaderSize;

            for (int i = 0; i < iLevel; i++)
            {
                Array.Copy(Data, iOldIndex, DataNew, iNewIndex, iCopySize);
                iOldIndex += iCopySize;
                iNewIndex += iCopySize;
            }
            for (int j = 0; j < iCopySize; j++)
            {
                DataNew[iNewIndex++] = bDefaultValue;
            }
            for (int i = iLevel; i < iDepth; i++)
            {
                Array.Copy(Data, iOldIndex, DataNew, iNewIndex, iCopySize);
                iOldIndex += iCopySize;
                iNewIndex += iCopySize;
            }
            // PFD.SetUserData(DataNew, true);
            Data = DataNew;
            ReplaceDepth(iDepth + 1);

            Debug.Assert(Data.Length == ExpectedLength);
        }

        public void SetBytes(int iLevel0, int iLevel1, int iWidth0, int iWidth1, int iHeight0, int iHeight1,
            byte bValue)
        {
            byte[] DataNew = new byte[Data.Length];
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
            BW.Write(BR.ReadBytes(iHeaderSize));

            for (int i = 0; i < iDepth; i++)
            {
                for (int j = 0; j < iWidth; j++)
                {
                    for (int k = 0; k < iHeight; k++)
                    {
                        for (int l = 0; l < iBytesPerObject; l++)
                        {
                            byte bOrig = BR.ReadByte();
                            byte bNew = bOrig;
                            if ((iLevel0 <= i) && (i <= iLevel1)
                             && (iWidth0 <= j) && (j <= iWidth1)
                             && (iHeight0 <= k) && (k <= iHeight1)
                            )
                                bNew = bValue;
                            BW.Write(bNew);
                        }
                    }
                }
            }
            PFD.SetUserData(DataNew, true);
            Data = DataNew;
        }

#endif
    }
}

