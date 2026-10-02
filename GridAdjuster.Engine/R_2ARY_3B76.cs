/***************************************************************************
 *   Copyright (C) 2006 by Andi8104                                        *
 *   Andi8104@arcor.de                                                     *
 *                                                                         *
 *   Additional programming:                                               *
 *   Copyright (C) 2008-2010 by Mootilda                                   *
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

// IPFD.Instance = 0x00003B76 must be handled separately from most 2D Arrays
// because the default value varies per record and by location within the record.
// Get this wrong, and there are problems placing the mailbox and garbage can.
namespace LotExpander
{
    // Water elevation
    public class R_2ARY_3B76
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
        private int iHeaderSize = 89;
        private const int iLotTilesPerNeighborhoodTile = 10;
        private const int iConvertTilesToVertices = 1;  // Add: tiles -> vertices; Subtract: vertices -> tiles
        private const int iBytesPerObject = 4;

        public R_2ARY_3B76(IPackageFile LotPackage, IPackedFileDescriptor Descriptor)
        {
            PFD = Descriptor;
            IPackedFile PF = LotPackage.Read(PFD);
            Data = PF.UncompressedData;

            uint uInst = PFD.Instance;
            if (Test_PrintDebugInfo)
                Debug.Print("2ARY Instance {0:X8}:", uInst);
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            for (int i = 0; i < iLeadingZeros; i++)
            {
                byte bDummy = BR.ReadByte();
                Debug.Assert(bDummy == 0);
            }
            uint uBlockID = BR.ReadUInt32();
            if (uBlockID != 0x6B943B43)
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 2ARY Instance 3B76: Block ID");

            int iBlockVersion = BR.ReadInt32();
            Debug.Assert(iBlockVersion == 1);   // ToDo: Determine whether other versions are known and handled correctly

            byte bBlockNameLen = (byte)BR.PeekChar();
            if ((bBlockNameLen & 0x80) != 0)
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 2ARY Instance 3B76: Block Name Length");
            string sBlockName = BR.ReadString();
            if (sBlockName != "c2DArray")
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 2ARY Instance 3B76: Block Name");
            int iIndex = iLeadingZeros + 4 /* uBlockID */ + 4 /* iBlockVersion */ + 1 /* bBlockNameLen */ + bBlockNameLen;

            Debug.Assert(iWidthIndex == iIndex);
            iWidthIndex = iIndex;
            iWidth = BR.ReadInt32();
            iIndex += 4;

            Debug.Assert(iHeightIndex == iIndex);
            iHeightIndex = iIndex;
            iHeight = BR.ReadInt32();
            iIndex += 4;

            Debug.Assert(iHeaderSize == iIndex);
            iHeaderSize = iIndex;

            if ((Width < 1) || (Width > LETools.MaxLotSize))
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 2ARY Instance 3B76: Width");
            if ((Height < 1) || (Height > LETools.MaxLotSize))
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 2ARY Instance 3B76: Height");

            if (Data.Length != ExpectedLength)
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid 2ARY Instance 3B76: Data Length");
        }

        private int ExpectedLength
        {
            get
            {
                return iWidth * iHeight * iBytesPerObject + iHeaderSize;
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
                    byte[] DataNew = new byte[Data.Length + iWidthDiff * iHeight * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    //Header
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    //Zeilenweise am Ende einfügen
                    for (int i = 0; i < iHeight; i++)
                    {
                        float Dat = 0;
                        for (int j = 0; j < iWidth; j++)
                        {
                            Dat = BR.ReadSingle();
                            BW.Write(Dat);
                        }
                        // Use the last value read as the default value
                        for (int j = 0; j < iWidthDiff; j++)
                        {
                            BW.Write(Dat);
                        }
                    }
                    Data = DataNew;
                    ReplaceWidth(value * iLotTilesPerNeighborhoodTile + iConvertTilesToVertices);
                }
                else if (iWidthDiff < 0)
                {
                    Test_AssertEpsilon = false;
                    byte[] DataNew = new byte[Data.Length + iWidthDiff * iHeight * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    for (int i = 0; i < iHeight; i++)
                    {
                        for (int j = 0; j < (iWidth + iWidthDiff); j++)
                        {
                            float Dat = BR.ReadSingle();
                            BW.Write(Dat);
                        }
                        for (int j = 0; j < (0 - iWidthDiff); j++)
                        {
                            float Dat = BR.ReadSingle();
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
                    byte[] DataNew = new byte[Data.Length + iWidthDiff * iHeight * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    //Header
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    //Zeilenweise am Anfang einfügen
                    for (int i = 0; i < iHeight; i++)
                    {
                        // Pre-read the first float, to use as the default value
                        float Dat = BR.ReadSingle();
                        // Since we pre-read the first float, write one additional value out here
                        for (int j = 0; j < iWidthDiff + 1; j++)
                        {
                            BW.Write(Dat);
                        }
                        // Since we pre-read the first float, we have one less value to read here
                        for (int j = 0; j < iWidth - 1; j++)
                        {
                            Dat = BR.ReadSingle();
                            BW.Write(Dat);
                        }
                    }
                    Data = DataNew;
                    ReplaceWidth(value * iLotTilesPerNeighborhoodTile + iConvertTilesToVertices);
                }
                else if (iWidthDiff < 0)
                {
                    Test_AssertEpsilon = false;
                    byte[] DataNew = new byte[Data.Length + iWidthDiff * iHeight * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    for (int i = 0; i < iHeight; i++)
                    {
                        for (int j = 0; j < (0 - iWidthDiff); j++)
                        {
                            float Dat = BR.ReadSingle();
                        }
                        for (int j = 0; j < (iWidth + iWidthDiff); j++)
                        {
                            float Dat = BR.ReadSingle();
                            BW.Write(Dat);
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
                    byte[] DataNew = new byte[Data.Length + iWidth * iHeightDiff * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    //Header
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    //Am Ende einfügen
                    float[] Dat = new float[iWidth];
                    for (int i = 0; i < iHeight; i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            Dat[j] = BR.ReadSingle();
                            BW.Write(Dat[j]);
                        }
                    }
                    // Use the last set of values read as the default values
                    for (int i = 0; i < iHeightDiff; i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            BW.Write(Dat[j]);
                        }
                    }
                    Data = DataNew;
                    ReplaceHeight(value * iLotTilesPerNeighborhoodTile + iConvertTilesToVertices);
                }
                else if (iHeightDiff < 0)
                {
                    Test_AssertEpsilon = false;
                    byte[] DataNew = new byte[Data.Length + iWidth * iHeightDiff * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    for (int i = 0; i < (iHeight + iHeightDiff); i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            float Dat = BR.ReadSingle();
                            BW.Write(Dat);
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
                    byte[] DataNew = new byte[Data.Length + iWidth * iHeightDiff * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    //Header
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    //Am Anfang einfügen
                    // Pre-read the first set of floats, to use as the default values
                    float[] Dat = new float[iWidth];
                    for (int j = 0; j < iWidth; j++)
                    {
                        Dat[j] = BR.ReadSingle();
                    }
                    // Since we pre-read the first set of floats, write one additional set out here
                    for (int i = 0; i < iHeightDiff + 1; i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            BW.Write(Dat[j]);
                        }
                    }
                    // Since we pre-read the first set of floats, we have one less set to read here
                    for (int i = 0; i < iHeight - 1; i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            Dat[j] = BR.ReadSingle();
                            BW.Write(Dat[j]);
                        }
                    }
                    Data = DataNew;
                    ReplaceHeight(value * iLotTilesPerNeighborhoodTile + iConvertTilesToVertices);
                }
                else if (iHeightDiff < 0)
                {
                    Test_AssertEpsilon = false;
                    byte[] DataNew = new byte[Data.Length + iWidth * iHeightDiff * iBytesPerObject];
                    BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                    BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                    BW.Write(BR.ReadBytes(iHeaderSize));
                    for (int i = 0; i < (0 - iHeightDiff); i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            float Dat = BR.ReadSingle();
                        }
                    }
                    for (int i = 0; i < (iHeight + iHeightDiff); i++)
                    {
                        for (int j = 0; j < iWidth; j++)
                        {
                            float Dat = BR.ReadSingle();
                            BW.Write(Dat);
                        }
                    }
                    Data = DataNew;
                    ReplaceHeight(value * iLotTilesPerNeighborhoodTile + iConvertTilesToVertices);
                }
            }
        }

        private byte[] bHeader = null;
        private float[,] fWater = null;

        public void BeginUpdate()
        {
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            bHeader = BR.ReadBytes(iHeaderSize);

            // Read everything in
            fWater = new float[iHeight, iWidth];
            if (Test_PrintDebugInfo)
                Debug.Print("2ARY before Smooth:");
            for (int i = 0; i < iHeight; i++)
            {
                string s = string.Format("{0,2}:", i);
                for (int j = 0; j < iWidth; j++)
                {
                    fWater[i, j] = BR.ReadSingle();
                    s = string.Format("{0} {1,6}", s, fWater[i, j]);
                }
                if (Test_PrintDebugInfo)
                    Debug.Print(s);
            }
        }

        public float[,] GetArray()
        {
            if ((null == bHeader) || (null == fWater))
            {
                Debug.Fail("BeginUpdate never called");
                BeginUpdate();
            }
            return fWater;
        }

        public void EndUpdate()
        {
            if ((null == bHeader) || (null == fWater))
                return;
            byte[] DataNew = new byte[Data.Length];
            Array.Copy(Data, DataNew, Data.Length);
            BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
            BW.Write(bHeader);

            // Write everything out
            if (Test_PrintDebugInfo)
                Debug.Print("2ARY after Smooth:");
            for (int i = 0; i < iHeight; i++)
            {
                string s = string.Format("{0,2}:", i);
                for (int j = 0; j < iWidth; j++)
                {
                    BW.Write(fWater[i, j]);
                    s = string.Format("{0} {1,6}", s, fWater[i, j]);
                }
                if (Test_PrintDebugInfo)
                    Debug.Print(s);
            }
            Data = DataNew;
            PFD.SetUserData(Data, true);
        }

        // Smooth width at a specified height
        private void SmoothWidth(int h)
        {
            int w = 0;
            float fPrev = fWater[h, w];
            // if (Test_PrintDebugInfo)
            //     Debug.Print("{0},{1} Value {2}", h, w, fWater[h, w]);
            for (int j = iLotTilesPerNeighborhoodTile; j < iWidth; j += iLotTilesPerNeighborhoodTile)
            {
                float fNext = fWater[h, j];
                float fIncrement = (fNext - fPrev) / iLotTilesPerNeighborhoodTile;
                for (int k = 1; k < iLotTilesPerNeighborhoodTile; k++)
                {
                    w = j - iLotTilesPerNeighborhoodTile + k;
                    float fNew = fPrev + k * fIncrement;
                    // When expanding, old and new values should be "close enough"
                    if (Test_AssertEpsilon)
                        Debug.Assert(LETools.FloatEqual(fWater[h, w], fNew));
                    // if (Test_PrintDebugInfo)
                    //     Debug.Print("{0},{1} From {2} To {3}", h, w, fWater[h, w], fNew);
                    fWater[h, w] = fNew;
                }
                /* if (Test_PrintDebugInfo)
                 * {
                 *     w = j;
                 *     Debug.Print("{0},{1} Value {2}", h, w, fWater[h, w]);
                 * } */
                fPrev = fNext;
            }
        }

        // Smooth height at a specified width
        private void SmoothHeight(int w)
        {
            int h = 0;
            float fPrev = fWater[h, w];
            // if (Test_PrintDebugInfo)
            //     Debug.Print("{0},{1} Value {2}", h, w, fWater[h, w]);
            for (int j = iLotTilesPerNeighborhoodTile; j < iHeight; j += iLotTilesPerNeighborhoodTile)
            {
                float fNext = fWater[j, w];
                float fIncrement = (fNext - fPrev) / iLotTilesPerNeighborhoodTile;
                for (int k = 1; k < iLotTilesPerNeighborhoodTile; k++)
                {
                    h = j - iLotTilesPerNeighborhoodTile + k;
                    float fNew = fPrev + k * fIncrement;
                    // When expanding, old and new values should be "close enough"
                    if (Test_AssertEpsilon)
                        Debug.Assert(LETools.FloatEqual(fWater[h, w], fNew));
                    // if (Test_PrintDebugInfo)
                    //     Debug.Print("{0},{1} From {2} To {3}", h, w, fWater[h, w], fNew);
                    fWater[h, w] = fNew;
                }
                /* if (Test_PrintDebugInfo)
                 * {
                 *     h = j;
                 *     Debug.Print("{0},{1} Value {2}", h, w, fWater[h, w]);
                 * } */
                fPrev = fNext;
            }
        }

        // Smooth subvertices between main vertices (at multiples of 10) at the edge of the lot
        public void SmoothEdges()
        {
            if ((null == bHeader) || (null == fWater))
            {
                Debug.Fail("BeginUpdate never called");
                BeginUpdate();
            }
            SmoothWidth(0);             // Smooth width for height = 0
            SmoothWidth(iHeight - 1);   // Smooth width for height = max
            SmoothHeight(0);            // Smooth height for width = 0
            SmoothHeight(iWidth - 1);   // Smooth height for width = max
        }

        public void ReplaceEdges(float fWaterTable, float fElevation, float[,] fHoodTerrain)
        {
            if ((null == bHeader) || (null == fWater))
            {
                Debug.Fail("BeginUpdate never called");
                BeginUpdate();
            }
            for (int i = 0; i < iHeight; i += iLotTilesPerNeighborhoodTile)
            {
                // Replace height for width = 0
                float f = fHoodTerrain[0, i / iLotTilesPerNeighborhoodTile] - fElevation;
                // if (f < fWaterTable)
                //     f = fWaterTable;
                if (Test_PrintDebugInfo)
                    Debug.Print("Water({0}, {1}) = {2} -> {3}", i, 0, fWater[i, 0], f);
                fWater[i, 0] = f;

                // Replace height for width = max
                int j = iWidth - 1;
                f = fHoodTerrain[j / iLotTilesPerNeighborhoodTile, i / iLotTilesPerNeighborhoodTile] - fElevation;
                // if (f < fWaterTable)
                //     f = fWaterTable;
                if (Test_PrintDebugInfo)
                    Debug.Print("Water({0}, {1}) = {2} -> {3}", i, j, fWater[i, j], f);
                fWater[i, j] = f;
            }
            for (int j = 0; j < iWidth; j += iLotTilesPerNeighborhoodTile)
            {
                // Replace width for height = 0
                float f = fHoodTerrain[j / iLotTilesPerNeighborhoodTile, 0] - fElevation;
                // if (f < fWaterTable)
                //     f = fWaterTable;
                if (Test_PrintDebugInfo)
                    Debug.Print("Water({0}, {1}) = {2} -> {3}", 0, j, fWater[0, j], f);
                fWater[0, j] = f;

                // Replace width for height = max
                int i = iHeight - 1;
                f = fHoodTerrain[j / iLotTilesPerNeighborhoodTile, i / iLotTilesPerNeighborhoodTile] - fElevation;
                // if (f < fWaterTable)
                //     f = fWaterTable;
                if (Test_PrintDebugInfo)
                    Debug.Print("Water({0}, {1}) = {2} -> {3}", i, j, fWater[i, j], f);
                fWater[i, j] = f;
            }
        }

        // Flatten the edge of the lot
        public void FlattenEdges(float fWaterTable, float fElevation)
        {
            if ((null == bHeader) || (null == fWater))
            {
                Debug.Fail("BeginUpdate never called");
                BeginUpdate();
            }
            // if (fElevation < fWaterTable)
            //     fElevation = fWaterTable;
            for (int i = 0; i < iHeight; i += iLotTilesPerNeighborhoodTile)
            {
                if (bCheckAlreadyFlat)
                {
                    Debug.Assert(fWater[i, 0] == fElevation);
                    Debug.Assert(fWater[i, iWidth - 1] == fElevation);
                }
                fWater[i, 0] = fElevation;              // Replace height for width = 0
                fWater[i, iWidth - 1] = fElevation;     // Replace height for width = max
            }
            for (int j = 0; j < iWidth; j += iLotTilesPerNeighborhoodTile)
            {
                if (bCheckAlreadyFlat)
                {
                    Debug.Assert(fWater[0, j] == fElevation);
                    Debug.Assert(fWater[iHeight - 1, j] == fElevation);
                }
                fWater[0, j] = fElevation;              // Replace width for height = 0
                fWater[iHeight - 1, j] = fElevation;    // Replace width for height = max
            }
        }

        // Flatten a section of the lot
        public void FlattenTerrain(float fWaterTable, float fElevation,
            int iWidthLow, int iWidthHigh, int iHeightLow, int iHeightHigh)
        {
            iWidthLow *= iLotTilesPerNeighborhoodTile;
            iWidthHigh *= iLotTilesPerNeighborhoodTile;
            iHeightLow *= iLotTilesPerNeighborhoodTile;
            iHeightHigh *= iLotTilesPerNeighborhoodTile;
            if ((null == bHeader) || (null == fWater))
            {
                Debug.Fail("BeginUpdate never called");
                BeginUpdate();
            }
            // if (fElevation < fWaterTable)
            //     fElevation = fWaterTable;
            for (int i = iHeightLow; i <= iHeightHigh; i++)
            {
                for (int j = iWidthLow; j <= iWidthHigh; j++)
                {
                    fWater[i, j] = fElevation;
                }
            }
        }

        // Use Neighborhood Terrain to determine elevation of a portion of a lot
        public void ReplaceTerrain(float fWaterTable, float fElevation, float[,] fHoodTerrain,
            int iHoodWidthLow, int iHoodWidthHigh, int iHoodHeightLow, int iHoodHeightHigh)
        {
            int iLotWidthLow = iHoodWidthLow * iLotTilesPerNeighborhoodTile;
            int iLotWidthHigh = iHoodWidthHigh * iLotTilesPerNeighborhoodTile;
            int iLotHeightLow = iHoodHeightLow * iLotTilesPerNeighborhoodTile;
            int iLotHeightHigh = iHoodHeightHigh * iLotTilesPerNeighborhoodTile;

            if ((null == bHeader) || (null == fWater))
            {
                Debug.Fail("BeginUpdate never called");
                BeginUpdate();
            }
            for (int iH = iHoodWidthLow, iL = iLotWidthLow;
                iH <= iHoodWidthHigh;
                iH++, iL += iLotTilesPerNeighborhoodTile)
            {
                for (int jH = iHoodHeightLow, jL = iLotHeightLow;
                jH <= iHoodHeightHigh;
                jH++, jL += iLotTilesPerNeighborhoodTile)
                {
                    float f00 = fHoodTerrain[iH, jH] - fElevation;
                    // if (f00 < fWaterTable)
                    //     f00 = fWaterTable;
                    fWater[jL, iL] = f00;

                    // if ((iHoodWidthHigh == iH) || (iHoodHeightHigh == jH))
                    //     continue;   // No next major vertex

                    for (int i = 0; i < iLotTilesPerNeighborhoodTile; i++)
                    {
                        float dX = i / 10.0F;
                        int iN = iH + ((iHoodWidthHigh == iH) ? 0 : 1);
                        for (int j = 0; j < iLotTilesPerNeighborhoodTile; j++)
                        {
                            float dY = j / 10.0F;
                            int jN = jH + ((iHoodHeightHigh == jH) ? 0 : 1);
                            float f01 = fHoodTerrain[iH, jN];
                            float f10 = fHoodTerrain[iN, jH];
                            float f11 = fHoodTerrain[iN, jN];

                            // Smooth out terrain by having intermediate points tend towards the closest vertices.
                            float f = dY * ((dX * f11) + ((1 - dX) * f01))
                                    + (1 - dY) * ((dX * f10) + ((1 - dX) * f00));
                            fWater[((iHoodHeightHigh == jH) ? jL : jL + j), ((iHoodWidthHigh == iH) ? iL : iL + i)] = f;
                        }
                    }
                }
            }
        }
#endif

#if ! ADJUST
        public void Flatten(int iWidth0, int iWidth1, int iHeight0, int iHeight1, bool bRelative, float fOffset)
        {
            byte[] DataNew = new byte[Data.Length];
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
            BW.Write( BR.ReadBytes(iHeaderSize));

            for (int k = 0; k < iHeight; k++)
            {
                for (int j = 0; j < iWidth; j++)
                {
                    float fOrig = BR.ReadSingle();
                    float fNew = fOrig;
                    if ((iWidth0 <= j) && (j <= iWidth1)
                     && (iHeight0 <= k) && (k <= iHeight1)
                    )
                        fNew = fOffset + ((bRelative) ? fOrig : -0.5F);
                    BW.Write(fNew);
                }
            }
            PFD.SetUserData(DataNew, true);
            Data = DataNew;
        }
#endif
    }
}
