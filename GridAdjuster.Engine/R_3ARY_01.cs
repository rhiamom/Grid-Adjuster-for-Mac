/***************************************************************************
 *   Copyright (C) 2006 by Andi8104                                        *
 *   Andi8104@arcor.de                                                     *
 *                                                                         *
 *   Additional programming:                                               *
 *   Copyright (C) 2008 by Mootilda                                        *
 *   http://Mootilda.ModTheSims2.com                                       *
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

// IPFD.Instance = 0x00000001 must be handled separately from most 3D Arrays
// because the default value varies per record and by location within the record.
namespace LotExpander
{
    // Grid elevation, including terrain at ground level
    public class R_3ARY_01
    {
        private bool Test_PrintDebugInfo = false;   // Enable (T) or disable (F) printing of debug information
        private bool Test_AssertEpsilon = false;    // Enable (T) or disable (F) assert on > epsilon
                                                    // Turns off automatically when shrinking
        private bool bCheckAlreadyFlat = false;     // Enable (T) or disable (F) assert if not flat

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
        private const int iConvertTilesToVertices = 1;  // Add: tiles -> vertices; Subtract: vertices -> tiles
        private int iBytesPerObject = 4;

        public R_3ARY_01(IPackageFile LotPackage, IPackedFileDescriptor Descriptor)
        {
            PFD = Descriptor;
            IPackedFile PF = LotPackage.Read(PFD);
            Data = PF.UncompressedData;

            uint uInst = PFD.Instance;
            if (Test_PrintDebugInfo)
                Debug.Print("3ARY Instance {0:X8}:", uInst);
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            for (int i = 0; i < iLeadingZeros; i++)
            {
                byte bDummy = BR.ReadByte();
                Debug.Assert(bDummy == 0);
            }
            uint uBlockID = BR.ReadUInt32();
            if (uBlockID != 0x2A51171B)
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 3ARY Instance 1: Block ID");

            int iBlockVersion = BR.ReadInt32();
            Debug.Assert(iBlockVersion == 1);   // ToDo: Determine whether other versions are known and handled correctly

            byte bBlockNameLen = (byte)BR.PeekChar();
            if ((bBlockNameLen & 0x80) != 0)
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 3ARY Instance 1: Block Name Length");
            string sBlockName = BR.ReadString();
            if (sBlockName != "c3DArray")
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 3ARY Instance 1: Block Name");
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

            if ((Width < 1) || (Width > LETools.MaxLotSize))
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 3ARY Instance 1: Width");
            if ((Height < 1) || (Height > LETools.MaxLotSize))
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 3ARY Instance 1: Height");
            if (Depth < 1)  // ToDo: What are valid values for Depth?
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 3ARY Instance 1: Depth");

            if (Data.Length != ExpectedLength)
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 3ARY Instance 1: Data Length");
        }

        private int ExpectedLength
        {
            get
            {
                return iWidth * iHeight * iDepth * iBytesPerObject + iHeaderSize;
            }
        }

#if ADJUST
        private void ReplaceInt(int i, int iIndex)
        {
            byte[] BA = new byte[4];
            BinaryWriter BW = new BinaryWriter(new MemoryStream(BA));
            BW.Write(i);
            Array.Copy(BA, 0, Data, iIndex, 4);
            PFD.SetUserData(Data, true);

            Debug.Assert(Data.Length == ExpectedLength);
        }

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

        public int Width
        {
            get
            {
                return (iWidth - iConvertTilesToVertices) / iLotTilesPerNeighborhoodTile;
            }
#if ADJUST
            set
            {
                int iWidthDiff = (value - this.Width) * iLotTilesPerNeighborhoodTile;
                if (iWidthDiff > 0)
                {
                    byte[] DataNew = new byte[Data.Length + iWidthDiff * iHeight * iDepth * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    //Header
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    //Am Ende einfügen
                    for (int i = 0; i < iDepth; i++)
                    {
                        float[] Dat = new float[iHeight];
                        for (int j = 0; j < iWidth; j++)
                        {
                            for (int k = 0; k < iHeight; k++)
                            {
                                Dat[k] = BR.ReadSingle();
                                BW.Write(Dat[k]);
                            }
                        }
                        // Use the last set of values read as the default values
                        for (int j = 0; j < iWidthDiff; j++)
                        {
                            for (int k = 0; k < iHeight; k++)
                            {
                                BW.Write(Dat[k]);
                            }
                        }
                    }
                    Data = DataNew;
                    ReplaceWidth(value * iLotTilesPerNeighborhoodTile + iConvertTilesToVertices);
                }
                else if (iWidthDiff < 0)
                {
                    Test_AssertEpsilon = false;
                    byte[] DataNew = new byte[Data.Length + iWidthDiff * iHeight * iDepth * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    for (int i = 0; i < iDepth; i++)
                    {
                        for (int j = 0; j < (iWidth + iWidthDiff); j++)
                        {
                            for (int k = 0; k < iHeight; k++)
                            {
                                float Dat = BR.ReadSingle();
                                BW.Write(Dat);
                            }
                        }
                        for (int j = 0; j < (0 - iWidthDiff); j++)
                        {
                            for (int k = 0; k < iHeight; k++)
                            {
                                float Dat = BR.ReadSingle();
                            }
                        }
                    }
                    Data = DataNew;
                    ReplaceWidth(value * iLotTilesPerNeighborhoodTile + iConvertTilesToVertices);
                }
            }
#endif
        }

#if ADJUST
        public int WidthRev
        {
            set
            {
                int iWidthDiff = (value - this.Width) * iLotTilesPerNeighborhoodTile;
                if (iWidthDiff > 0)
                {
                    byte[] DataNew = new byte[Data.Length + iWidthDiff * iHeight * iDepth * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    //Header
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    //Am Anfang einfügen
                    for (int i = 0; i < iDepth; i++)
                    {
                        // Pre-read the first set of floats, to use as the default values
                        float[] Dat = new float[iHeight];
                        for (int k = 0; k < iHeight; k++)
                        {
                            Dat[k] = BR.ReadSingle();
                        }
                        // Since we pre-read the first set of floats, write one additional set out here
                        for (int j = 0; j < iWidthDiff + 1; j++)
                        {
                            for (int k = 0; k < iHeight; k++)
                            {
                                BW.Write(Dat[k]);
                            }
                        }
                        // Since we pre-read the first set of floats, we have one less set to read here
                        for (int j = 0; j < iWidth - 1; j++)
                        {
                            for (int k = 0; k < iHeight; k++)
                            {
                                Dat[k] = BR.ReadSingle();
                                BW.Write(Dat[k]);
                            }
                        }
                    }
                    Data = DataNew;
                    ReplaceWidth(value * iLotTilesPerNeighborhoodTile + iConvertTilesToVertices);
                }
                else if (iWidthDiff < 0)
                {
                    Test_AssertEpsilon = false;
                    byte[] DataNew = new byte[Data.Length + iWidthDiff * iHeight * iDepth * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    for (int i = 0; i < iDepth; i++)
                    {
                        for (int j = 0; j < (0 - iWidthDiff); j++)
                        {
                            for (int k = 0; k < iHeight; k++)
                            {
                                float Dat = BR.ReadSingle();
                            }
                        }
                        for (int j = 0; j < (iWidth + iWidthDiff); j++)
                        {
                            for (int k = 0; k < iHeight; k++)
                            {
                                float Dat = BR.ReadSingle();
                                BW.Write(Dat);
                            }
                        }
                    }
                    Data = DataNew;
                    ReplaceWidth(value * iLotTilesPerNeighborhoodTile + iConvertTilesToVertices);
                }
            }
        }
#endif

        public int Height
        {
            get
            {
                return (iHeight - iConvertTilesToVertices) / iLotTilesPerNeighborhoodTile;
            }
#if ADJUST
            set
            {
                int iHeightDiff = (value - this.Height) * iLotTilesPerNeighborhoodTile;
                if (iHeightDiff > 0)
                {
                    byte[] DataNew = new byte[Data.Length + iWidth * iHeightDiff * iDepth * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    //Header
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    //Spaltenweise am Ende einfügen
                    for (int i = 0; i < iDepth; i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            float Dat = 0;
                            for (int k = 0; k < iHeight; k++)
                            {
                                Dat = BR.ReadSingle();
                                BW.Write(Dat);
                            }
                            // Use the last value read as the default value
                            for (int k = 0; k < iHeightDiff; k++)
                            {
                                BW.Write(Dat);
                            }
                        }
                    }
                    Data = DataNew;
                    ReplaceHeight(value * iLotTilesPerNeighborhoodTile + iConvertTilesToVertices);
                }
                else if (iHeightDiff < 0)
                {
                    Test_AssertEpsilon = false;
                    byte[] DataNew = new byte[Data.Length + iWidth * iHeightDiff * iDepth * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    for (int i = 0; i < iDepth; i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            for (int k = 0; k < (iHeight + iHeightDiff); k++)
                            {
                                float Dat = BR.ReadSingle();
                                BW.Write(Dat);
                            }
                            for (int k = 0; k < (0 - iHeightDiff); k++)
                            {
                                float Dat = BR.ReadSingle();
                            }
                        }
                    }
                    Data = DataNew;
                    ReplaceHeight(value * iLotTilesPerNeighborhoodTile + iConvertTilesToVertices);
                }
            }
#endif
        }

#if ADJUST
        public int HeightRev
        {
            set
            {
                int iHeightDiff = (value - this.Height) * iLotTilesPerNeighborhoodTile;
                if (iHeightDiff > 0)
                {
                    byte[] DataNew = new byte[Data.Length + iWidth * iHeightDiff * iDepth * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    //Header
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    //Spaltenweise am Anfang einfügen
                    for (int i = 0; i < iDepth; i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            // Pre-read the first float, to use as the default value
                            float Dat = BR.ReadSingle();
                            // Since we pre-read the first float, write one additional value out here
                            for (int k = 0; k < iHeightDiff + 1; k++)
                            {
                                BW.Write(Dat);
                            }
                            // Since we pre-read the first float, we have one less value to read here
                            for (int k = 0; k < iHeight - 1; k++)
                            {
                                Dat = BR.ReadSingle();
                                BW.Write(Dat);
                            }
                        }
                    }
                    Data = DataNew;
                    ReplaceHeight(value * iLotTilesPerNeighborhoodTile + iConvertTilesToVertices);
                }
                else if (iHeightDiff < 0)
                {
                    Test_AssertEpsilon = false;
                    byte[] DataNew = new byte[Data.Length + iWidth * iHeightDiff * iDepth * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    for (int i = 0; i < iDepth; i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            for (int k = 0; k < (0 - iHeightDiff); k++)
                            {
                                float Dat = BR.ReadSingle();
                            }
                            for (int k = 0; k < (iHeight + iHeightDiff); k++)
                            {
                                float Dat = BR.ReadSingle();
                                BW.Write(Dat);
                            }
                        }
                    }
                    Data = DataNew;
                    ReplaceHeight(value * iLotTilesPerNeighborhoodTile + iConvertTilesToVertices);
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

#if ADJUST
        private byte[] bHeader = null;
        private float[, ,] fLotTerrain = null;

        public void BeginUpdate()
        {
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            bHeader = BR.ReadBytes(iHeaderSize);

            // Read everything in
            fLotTerrain = new float[iDepth, iWidth, iHeight];
            if (Test_PrintDebugInfo)
                Debug.Print("3ARY Instance 1 before Adjustments:");
            for (int i = 0; i < iDepth; i++)
            {
                if (Test_PrintDebugInfo)
                    Debug.Print("Depth = {0}:", i);
                for (int j = 0; j < iWidth; j++)
                {
                    string s = string.Format("{0,2}:", j);
                    for (int k = 0; k < iHeight; k++)
                    {
                        fLotTerrain[i, j, k] = BR.ReadSingle();
                        s = string.Format("{0} {1,6}", s, fLotTerrain[i, j, k]);
                    }
                    if (Test_PrintDebugInfo)
                        Debug.Print(s);
                }
            }
        }

        public float[, ,] GetArray()
        {
            if ((null == bHeader) || (null == fLotTerrain))
            {
                Debug.Fail("BeginUpdate never called");
                BeginUpdate();
            }
            return fLotTerrain;
        }

        public void EndUpdate()
        {
            if ((null == bHeader) || (null == fLotTerrain))
                return;

            byte[] DataNew = new byte[Data.Length];
            Array.Copy(Data, DataNew, Data.Length);
            BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
            BW.Write(bHeader);

            // Write everything out
            if (Test_PrintDebugInfo)
                Debug.Print("3ARY Instance 1 after Adjustments:");
            for (int i = 0; i < iDepth; i++)
            {
                if (Test_PrintDebugInfo)
                    Debug.Print("Depth = {0}:", i);
                for (int j = 0; j < iWidth; j++)
                {
                    string s = string.Format("{0,2}:", j);
                    for (int k = 0; k < iHeight; k++)
                    {
                        BW.Write(fLotTerrain[i, j, k]);
                        s = string.Format("{0} {1,6}", s, fLotTerrain[i, j, k]);
                    }
                    if (Test_PrintDebugInfo)
                        Debug.Print(s);
                }
            }
            Data = DataNew;
            PFD.SetUserData(Data, true);
        }

        // Return the lot elevation at a specific point
        public float Elevation(int d, int w, int h)
        {
            if ((null == bHeader) || (null == fLotTerrain))
            {
                Debug.Fail("BeginUpdate never called");
                BeginUpdate();
            }
            w *= iLotTilesPerNeighborhoodTile;
            h *= iLotTilesPerNeighborhoodTile;
            return fLotTerrain[d, w, h];
        }

        // Smooth width at a specified depth and height
        private void SmoothWidth(int d, int h)
        {
            int w = 0;
            float fPrev = fLotTerrain[d, w, h];
            // if (Test_PrintDebugInfo)
            //     Debug.Print("{0},{1},{2} Value {3}", d, w, h, fLotTerrain[d, w, h]);
            for (int j = iLotTilesPerNeighborhoodTile; j < iWidth; j += iLotTilesPerNeighborhoodTile)
            {
                float fNext = fLotTerrain[d, j, h];
                float fIncrement = (fNext - fPrev) / iLotTilesPerNeighborhoodTile;
                for (int k = 1; k < iLotTilesPerNeighborhoodTile; k++)
                {
                    w = j - iLotTilesPerNeighborhoodTile + k;
                    float fNew = fPrev + k * fIncrement;
                    // When expanding, old and new values should be "close enough"
                    if (Test_AssertEpsilon)
                        Debug.Assert(LETools.FloatEqual(fLotTerrain[d, w, h], fNew));
                    // if (Test_PrintDebugInfo)
                    //     Debug.Print("{0},{1},{2} From {3} To {4}", d, w, h, fLotTerrain[d, w, h], fNew);
                    fLotTerrain[d, w, h] = fNew;
                }
                /* if (Test_PrintDebugInfo)
                 * {
                 *     w = j;
                 *     Debug.Print("{0},{1},{2} Value {3}", d, w, h, fLotTerrain[d, w, h]);
                 * } */
                fPrev = fNext;
            }
        }

        // Smooth height at a specified depth and width
        private void SmoothHeight(int d, int w)
        {
            int h = 0;
            float fPrev = fLotTerrain[d, w, h];
            // if (Test_PrintDebugInfo)
            //     Debug.Print("{0},{1},{2} Value {3}", d, w, h, fLotTerrain[d, w, h]);
            for (int j = iLotTilesPerNeighborhoodTile; j < iHeight; j += iLotTilesPerNeighborhoodTile)
            {
                float fNext = fLotTerrain[d, w, j];
                float fIncrement = (fNext - fPrev) / iLotTilesPerNeighborhoodTile;
                for (int k = 1; k < iLotTilesPerNeighborhoodTile; k++)
                {
                    h = j - iLotTilesPerNeighborhoodTile + k;
                    float fNew = fPrev + k * fIncrement;
                    // When expanding, old and new values should be "close enough"
                    if (Test_AssertEpsilon)
                        Debug.Assert(LETools.FloatEqual(fLotTerrain[d, w, h], fNew));
                    // if (Test_PrintDebugInfo)
                    //     Debug.Print("{0},{1},{2} From {3} To {4}", d, w, h, fLotTerrain[d, w, h], fNew);
                    fLotTerrain[d, w, h] = fNew;
                }
                /* if (Test_PrintDebugInfo)
                 * {
                 *     h = j;
                 *     Debug.Print("{0},{1},{2} Value {3}", d, w, h, fLotTerrain[d, w, h]);
                 * } */
                fPrev = fNext;
            }
        }

        // Smooth subvertices between main vertices (at multiples of 10) at the edge of the lot
        public void SmoothEdges(int iGroundLevel)
        {
            if ((null == bHeader) || (null == fLotTerrain))
            {
                Debug.Fail("BeginUpdate never called");
                BeginUpdate();
            }
            int i = iGroundLevel;           // Process depth = ground
            SmoothWidth(i, 0);              // Smooth width for height = 0
            SmoothWidth(i, iHeight - 1);    // Smooth width for height = max
            SmoothHeight(i, 0);             // Smooth height for width = 0
            SmoothHeight(i, iWidth - 1);    // Smooth height for width = max
        }

        // Use Neighborhood Terrain to determine elevation at edge of lot
        public void ReplaceEdges(int iGroundLevel, float[,] fHoodTerrain)
        {
            if ((null == bHeader) || (null == fLotTerrain))
            {
                Debug.Fail("BeginUpdate never called");
                BeginUpdate();
            }
            int i = iGroundLevel;   // Process depth = ground
            for (int j = 0; j < iWidth; j += iLotTilesPerNeighborhoodTile)
            {
                // Replace width for height = 0
                float f = fHoodTerrain[j / iLotTilesPerNeighborhoodTile, 0];
                if (Test_PrintDebugInfo)
                    Debug.Print("Lot Terrain({0}, {1}, {2}) = {3} -> {4}", i, j, 0, fLotTerrain[i, j, 0], f);
                fLotTerrain[i, j, 0] = f;

                // Replace width for height = max
                int k = iHeight - 1;
                f = fHoodTerrain[j / iLotTilesPerNeighborhoodTile, k / iLotTilesPerNeighborhoodTile];
                if (Test_PrintDebugInfo)
                    Debug.Print("Lot Terrain({0}, {1}, {2}) = {3} -> {4}", i, j, k, fLotTerrain[i, j, k], f);
                fLotTerrain[i, j, k] = f;
            }
            for (int k = 0; k < iHeight; k += iLotTilesPerNeighborhoodTile)
            {
                // Replace height for width = 0
                float f = fHoodTerrain[0, k / iLotTilesPerNeighborhoodTile];
                if (Test_PrintDebugInfo)
                    Debug.Print("Lot Terrain({0}, {1}, {2}) = {3} -> {4}", i, 0, k, fLotTerrain[i, 0, k], f);
                fLotTerrain[i, 0, k] = f;

                // Replace height for width = max
                int j = iWidth - 1;
                f = fHoodTerrain[j / iLotTilesPerNeighborhoodTile, k / iLotTilesPerNeighborhoodTile];
                if (Test_PrintDebugInfo)
                    Debug.Print("Lot Terrain({0}, {1}, {2}) = {3} -> {4}", i, j, k, fLotTerrain[i, j, k], f);
                fLotTerrain[i, j, k] = f;
            }
        }

        // Flatten the edge of the lot
        public void FlattenEdges(int iGroundLevel, float fElevation)
        {
            if ((null == bHeader) || (null == fLotTerrain))
            {
                Debug.Fail("BeginUpdate never called");
                BeginUpdate();
            }
            int i = iGroundLevel;                               // Process depth = ground
            for (int j = 0; j < iWidth; j += iLotTilesPerNeighborhoodTile)
            {
                if (bCheckAlreadyFlat)
                {
                    Debug.Assert(fLotTerrain[i, j, 0] == fElevation);
                    Debug.Assert(fLotTerrain[i, j, iHeight - 1] == fElevation);
                }
                fLotTerrain[i, j, 0] = fElevation;              // Replace width for height = 0
                fLotTerrain[i, j, iHeight - 1] = fElevation;    // Replace width for height = max
            }
            for (int k = 0; k < iHeight; k += iLotTilesPerNeighborhoodTile)
            {
                if (bCheckAlreadyFlat)
                {
                    Debug.Assert(fLotTerrain[i, 0, k] == fElevation);
                    Debug.Assert(fLotTerrain[i, iWidth - 1, k] == fElevation);
                }
                fLotTerrain[i, 0, k] = fElevation;              // Replace height for width = 0
                fLotTerrain[i, iWidth - 1, k] = fElevation;     // Replace height for width = max
            }
        }

        // Flatten a section of the lot
        public void FlattenTerrain(int iGroundLevel, float fElevation,
            int iWidthLow, int iWidthHigh, int iHeightLow, int iHeightHigh)
        {
            iWidthLow   *= iLotTilesPerNeighborhoodTile;
            iWidthHigh  *= iLotTilesPerNeighborhoodTile;
            iHeightLow  *= iLotTilesPerNeighborhoodTile;
            iHeightHigh *= iLotTilesPerNeighborhoodTile;

            if ((null == bHeader) || (null == fLotTerrain))
            {
                Debug.Fail("BeginUpdate never called");
                BeginUpdate();
            }
            int i = iGroundLevel;   // Process depth = ground
            for (int j = iWidthLow; j <= iWidthHigh; j++)
            {
                for (int k = iHeightLow; k <= iHeightHigh; k++)
                {
                    // Debug.Print("3ARY Instance 1: ({0},{1},{2}) = {3} -> 0", i, j, k, fElevation);
                    fLotTerrain[i, j, k] = fElevation;
                }
            }
        }

        // Use Neighborhood Terrain to determine elevation of a portion of a lot
        public void ReplaceTerrain(int iGroundLevel, float[,] fHoodTerrain,
            int iHoodWidthLow, int iHoodWidthHigh, int iHoodHeightLow, int iHoodHeightHigh)
        {
            int iLotWidthLow = iHoodWidthLow * iLotTilesPerNeighborhoodTile;
            int iLotWidthHigh = iHoodWidthHigh * iLotTilesPerNeighborhoodTile;
            int iLotHeightLow = iHoodHeightLow * iLotTilesPerNeighborhoodTile;
            int iLotHeightHigh = iHoodHeightHigh * iLotTilesPerNeighborhoodTile;

            if ((null == bHeader) || (null == fLotTerrain))
            {
                Debug.Fail("BeginUpdate never called");
                BeginUpdate();
            }
            int i = iGroundLevel;   // Process depth = ground

            // Replace the major vertices, then smooth between the major vertices
            // Note that this is a different algorithm than the straight-line edge smoothing
            for (int jH = iHoodWidthLow, jL = iLotWidthLow;
                jH <= iHoodWidthHigh;
                jH++, jL += iLotTilesPerNeighborhoodTile)
            {
                for (int kH = iHoodHeightLow, kL = iLotHeightLow;
                    kH <= iHoodHeightHigh;
                    kH++, kL += iLotTilesPerNeighborhoodTile)
                {
                    float f00 = fHoodTerrain[jH, kH];
                    // Debug.Print("3ARY Instance 1: ({0},{1},{2}) = {3} -> 0", i, jH, kH, f00);
                    fLotTerrain[i, jL, kL] = f00;

                    // if ((iHoodWidthHigh == jH) || (iHoodHeightHigh == kH))
                    //     continue;   // No next major vertex

                    for (int j = 0; j < iLotTilesPerNeighborhoodTile; j++)
                    {
                        float dX = j / 10.0F;
                        int jN = jH + ((iHoodWidthHigh == jH) ? 0 : 1);
                        for (int k = 0; k < iLotTilesPerNeighborhoodTile; k++)
                        {
                            float dY = k / 10.0F;
                            int kN = kH + ((iHoodHeightHigh == kH) ? 0 : 1);
                            float f01 = fHoodTerrain[jH, kN];
                            float f10 = fHoodTerrain[jN, kH];
                            float f11 = fHoodTerrain[jN, kN];

                            // Smooth out terrain by having intermediate points tend towards the closest vertices.
                            float f = dY       * ((dX * f11) + ((1 - dX) * f01))
                                    + (1 - dY) * ((dX * f10) + ((1 - dX) * f00));
                            fLotTerrain[i, ((iHoodWidthHigh == jH) ? jL : jL + j), ((iHoodHeightHigh == kH) ? kL : kL + k)] = f;
                        }
                    }
                }
            }
        }

        // Fix the hood terrain with the new lot edges
        public void FixHoodTerrain(int iGroundLevel, float[,] fHoodTerrain)
        {
            if ((null == bHeader) || (null == fLotTerrain))
            {
                Debug.Fail("BeginUpdate never called");
                BeginUpdate();
            }
Test_PrintDebugInfo = true;
            int i = iGroundLevel;   // Process depth = ground
            for (int j = 0; j < iWidth; j += iLotTilesPerNeighborhoodTile)
            {
                int jH = j / iLotTilesPerNeighborhoodTile;

                // Replace width for height = 0
                float f = fLotTerrain[i, j, 0];
                if (Test_PrintDebugInfo)
                    Debug.Print("Hood Terrain({0}, {1}) = {2} -> {3}", jH, 0, fHoodTerrain[jH, 0], f);
                fHoodTerrain[jH, 0] = f;

                // Replace width for height = max
                int k = iHeight - 1;
                int kH = k / iLotTilesPerNeighborhoodTile;

                f = fLotTerrain[i, j, k];
                if (Test_PrintDebugInfo)
                    Debug.Print("Hood Terrain({0}, {1}) = {2} -> {3}", jH, kH, fHoodTerrain[jH, kH], f);
                fHoodTerrain[jH, kH] = f;
            }
            for (int k = 0; k < iHeight; k += iLotTilesPerNeighborhoodTile)
            {
                int kH = k / iLotTilesPerNeighborhoodTile;

                // Replace height for width = 0
                float f = fLotTerrain[i, 0, k];
                if (Test_PrintDebugInfo)
                    Debug.Print("Hood Terrain({0}, {1}) = {2} -> {3}", 0, kH, fHoodTerrain[0, kH], f);
                fHoodTerrain[0, kH] = f;

                // Replace height for width = max
                int j = iWidth - 1;
                int jH = j / iLotTilesPerNeighborhoodTile;

                f = fLotTerrain[i, j, k];
                if (Test_PrintDebugInfo)
                    Debug.Print("Hood Terrain({0}, {1}) = {2} -> {3}", jH, kH, fHoodTerrain[jH, kH], f);
                fHoodTerrain[jH, kH] = f;
            }
Test_PrintDebugInfo = false;
        }
#endif

#if ! ADJUST
        public void Flatten(int iLevel0, int iLevel1, int iWidth0, int iWidth1, int iHeight0, int iHeight1,
            bool bRelative, float fOffset, float fMultiplier, bool bSkipMajorVertex)
        {
            byte[] DataNew = new byte[Data.Length];
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
            BW.Write( BR.ReadBytes(iHeaderSize));

            int jRem = 1;
            int kRem = 1;
            for (int i = 0; i < iDepth; i++)
            {
                for (int j = 0; j < iWidth; j++)
                {
                    for (int k = 0; k < iHeight; k++)
                    {
                        float fOrig = BR.ReadSingle();
                        float fNew = fOrig;
                        if ((iLevel0 <= i) && (i <= iLevel1)
                         && (iWidth0 <= j) && (j <= iWidth1)
                         && (iHeight0 <= k) && (k <= iHeight1)
                        )
                        {
                            if (bSkipMajorVertex)
                            {
                                Math.DivRem(j, 10, out jRem);
                                Math.DivRem(k, 10, out kRem);
                            }
                            if ((jRem == 0) && (kRem == 0))
                                fNew = fOrig;
                            else
                                fNew = fOffset + ((i - iLevel0) * fMultiplier) + ((bRelative) ? fOrig : 0F);
                        }
                        BW.Write(fNew);
                    }
                }
            }
            PFD.SetUserData(DataNew, true);
            Data = DataNew;
        }

        public void Slope(int iLevel0, int iLevel1, int iWidth0, int iWidth1, int iHeight0, int iHeight1,
            bool bRelative, float fOffset, float fMultiplier, int iRotate, float fToOffset)
        {
            byte[] DataNew = new byte[Data.Length];
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
            BW.Write(BR.ReadBytes(iHeaderSize));

            float fOffsetAdjust = 0F;
            // float fMultiplierAdjust = 0F;
            if ((0 == iRotate) || (180 == iRotate))
            {
                fOffsetAdjust = (fToOffset - fOffset) / (iHeight1 - iHeight0);
                // fMultiplierAdjust = (fMultiplier1 - fMultiplier) / (iHeight1 - iHeight0);
            }
            else if ((90 == iRotate) || (270 == iRotate))
            {
                fOffsetAdjust = (fToOffset - fOffset) / (iWidth1 - iWidth0);
                // fMultiplierAdjust = (fMultiplier1 - fMultiplier) / (iWidth1 - iWidth0);
            }
            else
            {
                // On any other angle, the corners will be take the end values.
                int iMax = Math.Max(iHeight1 - iHeight0, iWidth1 - iWidth0);
                fOffsetAdjust = (fToOffset - fOffset) / iMax;
                // fMultiplierAdjust = (fMultiplier1 - fMultiplier) / iMax;
            }
            for (int i = 0; i < iDepth; i++)
            {
                for (int j = 0; j < iWidth; j++)
                {
                    for (int k = 0; k < iHeight; k++)
                    {
                        float fOrig = BR.ReadSingle();
                        float fNew = fOrig;
                        if ((iLevel0 <= i) && (i <= iLevel1)
                         && (iWidth0 <= j) && (j <= iWidth1)
                         && (iHeight0 <= k) && (k <= iHeight1)
                        )
                        {
                            /*
                            float cosine = (float)Math.Cos(iRotate * Math.PI / 180.0);
                            float sine = (float)Math.Sin(iRotate * Math.PI / 180.0);
                            float j2 = j - (iWidth1 - iWidth0) / 2.0F;      // Translate width to center
                            float k2 = k - (iHeight1 - iHeight0) / 2.0F;    // Translate height to center
                            float x = j2 * cosine - k2 * sine;              // Rotate width
                            float y = j2 * sine + k2 * cosine;              // Rotate height
                            // No need to translate back from center?
                             */

                            float fLocation = 0;
                            if (0 == iRotate)
                                fLocation = k - iHeight0;
                            else if (90 == iRotate)
                                fLocation = j - iWidth0;
                            else if (180 == iRotate)
                                fLocation = iHeight1 - k;
                            else if (270 == iRotate)
                                fLocation = iWidth1 - j;
                            else
                            {
                                // ToDo: Implement general rotations
                                // fLocation = x - y;
                            }
                            fNew = fOffset + ((i - iLevel0) * fMultiplier) + ((bRelative) ? fOrig : 0F)
                                + (fOffsetAdjust * fLocation);
                        }
                        BW.Write(fNew);
                    }
                }
            }
            PFD.SetUserData(DataNew, true);
            Data = DataNew;
        }

        public void Curve(int iLevel0, int iLevel1, int iWidth0, int iWidth1, int iHeight0, int iHeight1,
            bool bRelative, float fOffset, float fMultiplier, int iRotate,
            float fAmplitude, float fPhaseShift, float fPeriod)
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
                        float fOrig = BR.ReadSingle();
                        float fNew = fOrig;
                        if ((iLevel0 <= i) && (i <= iLevel1)
                         && (iWidth0 <= j) && (j <= iWidth1)
                         && (iHeight0 <= k) && (k <= iHeight1)
                        )
                        {
                            /*
                            float cosine = (float)Math.Cos(iRotate * Math.PI / 180.0);
                            float sine = (float)Math.Sin(iRotate * Math.PI / 180.0);
                            float j2 = j - (iWidth1 - iWidth0) / 2.0F;      // Translate width to center
                            float k2 = k - (iHeight1 - iHeight0) / 2.0F;    // Translate height to center
                            float x = j2 * cosine - k2 * sine;              // Rotate width
                            float y = j2 * sine + k2 * cosine;              // Rotate height
                            // No need to translate back from center?
                             */

                            float fLocation = 0;
                            if (0 == iRotate)
                                fLocation = k - iHeight0;
                            else if (90 == iRotate)
                                fLocation = j - iWidth0;
                            else if (180 == iRotate)
                                fLocation = iHeight1 - k;
                            else if (270 == iRotate)
                                fLocation = iWidth1 - j;
                            else
                            {
                                // ToDo: Implement general rotations
                                // fLocation = x - y;
                            }
                            fLocation += fPhaseShift;   // Phase shift moves the entire curve forward or backwards
                            fLocation /= fPeriod;       // Period makes curve longer or shorter
                            fLocation *=
                                2.0F * (float)Math.PI;  // Make a period of 1 define a curve over 1 square (ie, flat)
                            float fSin = (float)Math.Sin(fLocation);
                            fNew = fOffset + ((i - iLevel0) * fMultiplier) + ((bRelative) ? fOrig : 0F)
                                + (fAmplitude * fSin);
                        }
                        BW.Write(fNew);
                    }
                }
            }
            PFD.SetUserData(DataNew, true);
            Data = DataNew;
        }

        public void HyperbolicParaboloid(
            int iLevel0, int iLevel1, int iWidth0, int iWidth1, int iHeight0, int iHeight1,
            bool bRelative, float fOffset, float fMultiplier, int iRotate,
            float fAmplitude, float fPhaseShift, float fPeriod)
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
                        float fOrig = BR.ReadSingle();
                        float fNew = fOrig;
                        if ((iLevel0 <= i) && (i <= iLevel1)
                         && (iWidth0 <= j) && (j <= iWidth1)
                         && (iHeight0 <= k) && (k <= iHeight1)
                        )
                        {
                            /*
                            // ToDo: Implement general rotations
                            float cosine = (float)Math.Cos(iRotate * Math.PI / 180.0);
                            float sine = (float)Math.Sin(iRotate * Math.PI / 180.0);
                            float j2 = j - (iWidth1 - iWidth0) / 2.0F;      // Translate width to center
                            float k2 = k - (iHeight1 - iHeight0) / 2.0F;    // Translate height to center
                            float x = j2 * cosine - k2 * sine;              // Rotate width
                            float y = j2 * sine + k2 * cosine;              // Rotate height
                            // No need to translate back from center?
                             */
                            float x = iWidth0 - j + (iWidth1 - iWidth0) / 2.0F;
                            float y = iHeight0 - k + (iHeight1 - iHeight0) / 2.0F;

                            // float fLocation = 0;
                            // fLocation += fPhaseShift;   // Phase shift moves the entire curve forward or backwards
                            // fLocation /= fPeriod;       // Period makes curve longer or shorter
                            // fLocation *=
                            //     2.0F * (float)Math.PI;  // Make a period of 1 define a curve over 1 square (ie, flat)
                            fNew = fOffset + ((i - iLevel0) * fMultiplier) + ((bRelative) ? fOrig : 0F);
                            float fHP = (x * y * 0.04F);
                            fNew += fHP;
                        }
                        BW.Write(fNew);
                    }
                }
            }
            PFD.SetUserData(DataNew, true);
            Data = DataNew;
        }
#endif
    }
}
