/***************************************************************************
 *   Copyright (C) 2006 by Andi8104                                        *
 *   Andi8104@arcor.de                                                     *
 *                                                                         *
 *   Additional programming:                                               *
 *   Copyright (C) 2007-2013 by Mootilda                                   *
 *   http://www.modthesims.info/member.php?u=589252                        *
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


namespace LotExpander
{
    public class R_OBJT
    {
        private bool Test_PrintDebugInfo = false;   // Enable (T) or disable (F) printing of debug information
        private bool Test_AssertDirection = false;  // Enable (T) or disable (F) assert on change of direction
        private bool Test_HandleExcess = false;     // Temporary: to allow handling of excess bytes at end of record
        private bool Test_PrintExcess = false;      // Temporary: to see whether any excess bytes remain after handling above

        private bool bChanged = false;
        private bool bPerson = false;

        private IPackedFileDescriptor PFD;
        private byte[] Data;
        private byte[] DataNew;
        private int WidthNew = 0;
        private int HeightNew = 0;
        private int iPersonVersion = 0;
        private int iLocomotableVersion = 0;
        private int iAnimatableVersion = 0;
        private int iObjectVersion = 0;

        struct Quaternion
        {
            public UInt32 qA;
            public UInt32 qB;
            public UInt32 qC;
            public UInt32 qD;
            public string Desc;

            public Quaternion(UInt32 uA, UInt32 uB, UInt32 uC, UInt32 uD, string sDesc)
            {
                qA = uA;
                qB = uB;
                qC = uC;
                qD = uD;
                Desc = sDesc;
            }
        }

        // From Portale.cs:
        private const byte Direction_Left        = 0x00;
        private const byte Direction_TopLeft     = 0x01;
        private const byte Direction_Top         = 0x02;
        private const byte Direction_TopRight    = 0x03;
        private const byte Direction_Right       = 0x04;
        private const byte Direction_BottomRight = 0x05;
        private const byte Direction_Bottom      = 0x06;
        private const byte Direction_BottomLeft  = 0x07;
        private const byte Direction_None        = 0x08;

        private byte Direction = Direction_None;
        private Quaternion[] Rotation;

        struct Coordinate
        {
            public float X;
            public float Y;
            public float Z;
            public Quaternion Rot;

            public Coordinate(float fX, float fY, float fZ, UInt32 uA, UInt32 uB, UInt32 uC, UInt32 uD)
            {
                X = fX;
                Y = fY;
                Z = fZ;
                Rot = new Quaternion(uA, uB, uC, uD, "");
            }
        }

        private const int Coordinate_Main     = 0;
        private const int Coordinate_Entry    = 1;
        private const int Coordinate_Animate1 = 2;
        private const int Coordinate_Animate2 = 3;
        private const int Coordinate_Animate3 = 4;
        private const int Coordinate_Animate4 = 5;
        private const int Coordinate_New      = 6;
        private const int Coordinate_Max      = 7;

        private Coordinate[] Coord;

        public R_OBJT(IPackageFile LotPackage, IPackedFileDescriptor Descriptor)
        {
            PFD = Descriptor;
            IPackedFile PF = LotPackage.Read(PFD);
            Data = PF.UncompressedData;

            Rotation = new Quaternion[Direction_None+1];  // X  Y  Z           W                               UInt64              Inge Jones
            Rotation[Direction_Left]        = new Quaternion(0, 0, 0x3F3504F3, 0x3F3504F3, "Left");         // 0x3F3504F33F3504F3 (0xF304353FF304353F)
            Rotation[Direction_TopLeft]     = new Quaternion(0, 0, 0x3EC3EF16, 0x3F6C835E, "Top Left");     // 0x3F6C835E3EC3EF16 (0x16EFC33E5E836C3F)
            Rotation[Direction_Top]         = new Quaternion(0, 0, 0x00000000, 0x3F800000, "Top");          // 0x3F80000000000000 (0x000000000000803F)
            Rotation[Direction_TopRight]    = new Quaternion(0, 0, 0x3EC3EF13, 0xBF6C835F, "Top Right");    // 0xBF6C835F3EC3EF13 (0x13EFC33E5F836CBF)
            Rotation[Direction_Right]       = new Quaternion(0, 0, 0x3F3504F2, 0xBF3504F4, "Right");        // 0xBF3504F43F3504F2 (0xF204353FF40435BF)
            Rotation[Direction_BottomRight] = new Quaternion(0, 0, 0x3F6C835E, 0xBEC3EF18, "Bottom Right"); // 0xBEC3EF183F6C835E (0x5E836C3F18EFC3BE)
            Rotation[Direction_Bottom]      = new Quaternion(0, 0, 0x3F800000, 0xB33BBD2E, "Bottom");       // 0xB33BBD2E3F800000 (0x0000803F2EBD3BB3)
            Rotation[Direction_BottomLeft]  = new Quaternion(0, 0, 0x3F6C835E, 0x3EC3EF15, "Bottom Left");  // 0x3EC3EF153F6C835E (0x5E836C3F15EFC33E)
            Rotation[Direction_None]        = new Quaternion(0, 0, 0, 0, "Non standard rotation");

            Coord = new Coordinate[Coordinate_Max];
        }

        private float XAddLow = 0;
        private float XAddHigh = 0;
        private float YAddLow  = 0;
        private float YAddHigh = 0;
        private bool bOnlyElevation = false;  // Change *only* the elevation, nothing else.
        private float fElevation = 0;
        private float fMinimum = 0;

#if ADJUST
        // Change object, excluding portals
        public void Change(int iXAddLow, int iXAddHigh, int iYAddLow, int iYAddHigh, int iWidthNew, int iHeightNew)
        {
            WidthNew = iWidthNew;
            HeightNew = iHeightNew;

            XAddLow  = (float)iXAddLow;
            XAddHigh = (float)iXAddHigh;
            YAddLow  = (float)iYAddLow;
            YAddHigh = (float)iYAddHigh;

            Change(false);
        }

        // Change portal
        public void Change(float fXAddLow, float fYAddLow, byte bDirection, int iWidthNew, int iHeightNew)
        {
            WidthNew = iWidthNew;
            HeightNew = iHeightNew;

            Direction = bDirection;

            XAddLow = fXAddLow;
            YAddLow = fYAddLow;

            Change(true);
        }

        // Change elevation of object
        public void Change(float fNewElevation)
        {
            bOnlyElevation = true;
            fElevation = fNewElevation;

            Change(false);
        }
#endif

#if ! ADJUST
        // Change elevation of object
        public void AddElevation(float fNewElevation, float fMinElevation)
        {
            bOnlyElevation = true;
            fElevation = fNewElevation;
            fMinimum = fMinElevation;

            Change(false);
        }
#endif

        // Change object, including portals
        // The difference between them is that a portal will be moved even if a coordinate is < 0
        private void Change(bool bPortal)
        {
            if (Test_PrintDebugInfo)
                Debug.Print("");
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);

            // ToDo: No need for this new array, since the length of the array doesn't change
            // Just replace the individual X and Y floats at the correct indexes.
            // The main problem is determining where all of the coordinates are in the record,
            // since there are so many 7BITSTR.
            DataNew = new byte[Data.Length];
            Array.Copy(Data, DataNew, Data.Length);
            BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
            for (int i = 0; i < 64; i++)
            {
                byte bDumm = BR.ReadByte();
                BW.Write(bDumm);
                Debug.Assert(bDumm == 0);
            }
            uint BlockID = BR.ReadUInt32();
            BW.Write(BlockID);
            switch (BlockID)
            {
                case 0xFA34F3EB: //cPerson
                    {
                        bPerson = true;
                        if (Test_PrintDebugInfo)
                            Debug.Print("  Block ID = FA34F3EB = cPerson");

                        iPersonVersion = BR.ReadInt32();
                        BW.Write(iPersonVersion);
                        Debug.Assert(iPersonVersion == 3);
                        if (Test_PrintDebugInfo)
                            Debug.Print("    cPerson Block Version = {0}", iPersonVersion);

                        byte bBlockNameLen = (byte)BR.PeekChar();
                        if ((bBlockNameLen & 0x80) != 0)
                            if (LETools.ErrorChecking)
                                throw new InvalidDataException("Invalid OBJT: cPerson Block Name Length");
                        string sBlockName = LETools.Copy7BitStr(BR, BW, false);
                        if (Test_PrintDebugInfo)
                            Debug.Print("    cPerson Block Name = {0}", sBlockName);
                        Debug.Assert(sBlockName == "cPerson");
                        if (sBlockName != "cPerson")
                            if (LETools.ErrorChecking)
                                throw new InvalidDataException("Invalid OBJT: Person Block Name");

                        BlockID = BR.ReadUInt32();
                        BW.Write(BlockID);
                        if (BlockID == 0xFD4F2EC7)
                        {
                            if (Test_PrintDebugInfo)
                                Debug.Print("    Block ID = 0xFD4F2EC7 = cLocomotable", BlockID);

                            iLocomotableVersion = BR.ReadInt32();
                            BW.Write(iLocomotableVersion);
                            Debug.Assert(iLocomotableVersion == 4);
                            if (Test_PrintDebugInfo)
                                Debug.Print("    cLocomotable Block Version = {0}", iLocomotableVersion);

                            bBlockNameLen = (byte)BR.PeekChar();
                            if ((bBlockNameLen & 0x80) != 0)
                                if (LETools.ErrorChecking)
                                    throw new InvalidDataException("Invalid OBJT: cLocomotable Block Name Length");
                            sBlockName = LETools.Copy7BitStr(BR, BW, false);
                            if (Test_PrintDebugInfo)
                                Debug.Print("    cLocomotable Block Name = {0}", sBlockName);
                            Debug.Assert(sBlockName == "cLocomotable");
                            if (sBlockName != "cLocomotable")
                                if (LETools.ErrorChecking)
                                    throw new InvalidDataException("Invalid OBJT: Locomotable Block Name");

                            BlockID = BR.ReadUInt32();
                            BW.Write(BlockID);
                        }
                        if (BlockID == 0x7D4F2BB8)
                        {
                            if (Test_PrintDebugInfo)
                                Debug.Print("    Block ID = 7D4F2BB8 = cAnimatable", BlockID);
                            cAnimatable(BR, BW, bPortal);
                        }
                        else if (LETools.ErrorChecking)
                            throw new InvalidDataException("Invalid OBJT: Block ID not cAnimatable (7D4F2BB8)");

                        break;
                    }
                case 0x7D4F2BB8: //cAnimatable
                    {
                        if (Test_PrintDebugInfo)
                            Debug.Print("  Block ID = 7D4F2BB8 = cAnimatable");
                        cAnimatable(BR, BW, bPortal);
                        break;
                    }
                case 0xFA1C39F7: //cObject
                    {
                        if (Test_PrintDebugInfo)
                            Debug.Print("  Block ID = FA1C39F7 = cObject");
                        cObject(BR, BW, bPortal);
                        break;
                    }
                default:
                    {
                        Debug.Fail("Unknown OBJT BlockID - May not be handled correctly");
                        if (LETools.ErrorChecking)
                            throw new InvalidDataException("Invalid OBJT: Block ID unknown");
                        break;
                    }
            }
            Data = DataNew;
            PFD.SetUserData(Data, true);
        }

        private void cObject(BinaryReader BR, BinaryWriter BW, bool bPortal)
        {
            //BR und BW müssen hinter BlockID stehn!
            iObjectVersion = BR.ReadInt32();
            BW.Write(iObjectVersion);
            if (Test_PrintDebugInfo)
                Debug.Print("    cObject Version = {0}", iObjectVersion);
            Debug.Assert((iObjectVersion == 15)
                      || (iObjectVersion == 16)
                      || (iObjectVersion == 17));

            byte bBlockNameLen = (byte)BR.PeekChar();
            if ((bBlockNameLen & 0x80) != 0)
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid OBJT: cObject Block Name Length");
            string sBlockName = LETools.Copy7BitStr(BR, BW, false);
            if (Test_PrintDebugInfo)
                Debug.Print("    cObject Block Name = {0}", sBlockName);
            Debug.Assert(sBlockName == "cObject");
            if (sBlockName != "cObject")
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid OBJT: cObject Block Name");

            string sModelName = LETools.Copy7BitStr(BR, BW, false);
            if (Test_PrintDebugInfo)
                Debug.Print("    cObject Model Name = {0}", sModelName);

            // Main entry
            int iCRESCount = BR.ReadInt32();
            BW.Write(iCRESCount);
            if (Test_PrintDebugInfo)
                Debug.Print("    cObject CRES Count = {0}", iCRESCount);
            for (int i = 0; i < iCRESCount; i++)
            {
                // CRES entry
                string sCRESName = LETools.Copy7BitStr(BR, BW, false);
                if (Test_PrintDebugInfo)
                    Debug.Print("      CRES Name = {0}", sCRESName);
                if (iObjectVersion == 17)
                {
                    int NType = BR.ReadInt32();
                    BW.Write(NType);
                    if (Test_PrintDebugInfo)
                        Debug.Print("      CRES NType = {0:X8}", NType);
                }
                int iMaterialMeshCount = BR.ReadInt32();
                BW.Write(iMaterialMeshCount);
                if (Test_PrintDebugInfo)
                    Debug.Print("      CRES Material/Mesh Count = {0}", iMaterialMeshCount);
                for (int j = 0; j < iMaterialMeshCount; j++)
                {
                    string sMaterial = LETools.Copy7BitStr(BR, BW, false);
                    string sMesh = LETools.Copy7BitStr(BR, BW, false);
                    if (Test_PrintDebugInfo)
                        Debug.Print("      CRES Material = \"{0}\" Mesh = \"{1}\"", sMaterial, sMesh);
                }
            }

            // Main entry
            bChanged = HandleEntry(BR, BW, true, Coordinate_Main, "    Object Main", sModelName, bPortal);

            int iEntryCount = BR.ReadInt32();
            BW.Write(iEntryCount);
            Debug.Assert(iEntryCount > 0);
            if (Test_PrintDebugInfo)
                Debug.Print("    Object Entry count = {0}", iEntryCount);

            // Change first entry; all others remain unchanged
            for (int i = 0; i < iEntryCount; i++) //Die 1 ist Absicht
            {
                string sName = LETools.Copy7BitStr(BR, BW, false);
                if (i == 0) // This is the only Z value which actually affects the location of the object
                    HandleEntry(BR, BW, true, Coordinate_Entry, string.Format("    Object Entry[{0}]", i), sName, bPortal);
                else
                    HandleEntry(BR, BW, false, Coordinate_Max, string.Format("    Object Entry[{0}]", i), sName, bPortal);
            }
            //Rest
            if (iObjectVersion > 15)
            {
                int iBlendPairCount = BR.ReadInt32();
                BW.Write(iBlendPairCount);
                if (Test_PrintDebugInfo)
                    Debug.Print("    Object Blend Pair Count = {0}", iBlendPairCount);
                for (int i = 0; i < iBlendPairCount; i++)
                {
                    string sBlendName = LETools.Copy7BitStr(BR, BW, false);
                    string sBlendPartner = LETools.Copy7BitStr(BR, BW, false);
                    int D = BR.ReadInt32();
                    BW.Write(D);
                    if (Test_PrintDebugInfo)
                        Debug.Print("      Blend Name = \"{0}\" Partner = \"{1}\" {2:X8}", sBlendName, sBlendPartner, D);
                }
            }
        }

        private void cAnimatable(BinaryReader BR, BinaryWriter BW, bool bPortal)
        {
            iAnimatableVersion = BR.ReadInt32();
            BW.Write(iAnimatableVersion);
            Debug.Assert((iAnimatableVersion == 14)
                      || (iAnimatableVersion == 15)
                      || (iAnimatableVersion == 16)
                      || (iAnimatableVersion == 17));
            if (Test_PrintDebugInfo)
                Debug.Print("    cAnimatable Version = {0}", iAnimatableVersion);

            byte bBlockNameLen = (byte)BR.PeekChar();
            if ((bBlockNameLen & 0x80) != 0)
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid OBJT: cAnimatable Block Name Length");
            string sBlockName = LETools.Copy7BitStr(BR, BW, false);
            if (Test_PrintDebugInfo)
                Debug.Print("    cAnimatable Block Name = {0}", sBlockName);
            Debug.Assert(sBlockName == "cAnimatable");
            if (sBlockName != "cAnimatable")
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid OBJT: cAnimatable Block Name");

            uint BlockID = BR.ReadUInt32();
            BW.Write(BlockID);
            if (BlockID == 0xFA1C39F7)
            {
                if (Test_PrintDebugInfo)
                    Debug.Print("  Block ID = FA1C39F7 = cObject");
                cObject(BR, BW, bPortal);
            }
            else if (LETools.ErrorChecking)
                throw new InvalidDataException("Invalid OBJT: Block ID not cObject (FA1C39F7)");

            float F = BR.ReadSingle();
            BW.Write(F);
            Debug.Assert((F == 1) || ((F >= .94F) && (F < .95F)));
            if (Test_PrintDebugInfo)
                Debug.Print("    Animatable F = {0}", F);

            int D1 = BR.ReadInt32();
            BW.Write(D1);
            Debug.Assert(D1 == 1);
            if (Test_PrintDebugInfo)
                Debug.Print("    Animatable D1 = {0}", D1);

            HandleEntry(BR, BW, true, Coordinate_Animate1, "    Animatable Entry 1", "", bPortal);
            HandleEntry(BR, BW, true, Coordinate_Animate2, "    Animatable Entry 2", "", bPortal);

            int iLen = 7;
            if (iAnimatableVersion < 16)
                iLen = 6;
            uint[] uDumm = new uint[iLen];
            for (int i = 0; i < iLen; i++)
            {
                uDumm[i] = BR.ReadUInt32();
                BW.Write(uDumm[i]);
            }
            if (Test_PrintDebugInfo)
            {
                if (iLen == 6)
                    Debug.Print("    Animatable unknown = {0:X8} {1:X8} {2:X8} {3:X8} {4:X8} {5:X8}",
                        uDumm[0], uDumm[1], uDumm[2], uDumm[3], uDumm[4], uDumm[5]);
                else
                    Debug.Print("    Animatable unknown = {0:X8} {1:X8} {2:X8} {3:X8} {4:X8} {5:X8} {6:X8}",
                        uDumm[0], uDumm[1], uDumm[2], uDumm[3], uDumm[4], uDumm[5], uDumm[6]);
            }

            // ToDo: Determine format of version 14.  See:
            //       Le Fiesta Tech:
            //          College Corner Market
            //          116 Aridestra Drive
            //          50, 54, 57-59 Dusty Drive
            //       Sim State U:
            //          124 Almond Road
/*          if( iAnimatableVersion == 14)
                HandleEntry(BR, BW, false, Coordinate_Animate3, "    Animatable Entry 3", "", bPortal);
            else
 */             HandleEntry(BR, BW, true,  Coordinate_Animate3, "    Animatable Entry 3", "", bPortal);

            if (iAnimatableVersion > 15)
            {
                uint uCount = BR.ReadUInt32();
                BW.Write(uCount);

                // If uCount > 1, then the second entry seems to have another set of coordinates.
                // However, haven't yet figured out this structure, so return before trying to parse.
                // Changing these coordinates manually removes "jumping", but does not remove crash.
                if (Test_HandleExcess)
                {
                    if (uCount != 0)
                    {
                        Debug.Print("");
                        if (Test_PrintDebugInfo)
                            Debug.Print("    Animatable Count = {0}", uCount);

                        // Determine the number of bytes left to process
                        byte[] DataTemp = new byte[Data.Length];
                        int iExcess = 0;
                        for (; ; )
                        {
                            try
                            {
                                byte B = BR.ReadByte();
                                DataTemp[iExcess++] = B;
                            }
                            catch
                            {
                                break;
                            }
                        }
                        if (iExcess == 0)
                            Debug.Fail("Count > 0 but no excess data!");
                        else if (bPerson)
                            Debug.Print("*** Person version {0} / {1} / {2} / {3} has {4} excess bytes", iPersonVersion, iLocomotableVersion, iAnimatableVersion, iObjectVersion, iExcess);
                        else
                            Debug.Print("*** Animatable version {0} / {1} has {2} excess bytes", iAnimatableVersion, iObjectVersion, iExcess);

                        // Now that we know how many bytes are left, set up a byte array and process them.
                        byte[] DataExcess = new byte[iExcess];
                        Array.Copy(DataTemp, DataExcess, iExcess);
                        DataTemp = null;
                        BR = SimPe.Helper.GetBinaryReader(DataExcess);
#if ADJUST
                        LETools.PrintData(DataExcess);
#endif

                        bool bReducedSize = false;

                        for (int i = 0; i < uCount; i++)
                        {
                            if (Test_PrintDebugInfo)
                                Debug.Print("    Animatable Entry {0}", i);

                            uint U1 = BR.ReadUInt32();
                            BW.Write(U1);
                            // Debug.Assert(((U1 == 0) || (U1 == 1) || (U1 == 2)), "U1 is bad");
                            if (U1 == 0)
                                continue;

                            uint U2 = BR.ReadUInt32();
                            BW.Write(U2);
                            // Debug.Assert(((U2 == 1) || (U2 == 0x09D0CE69)), "U2 is bad");

                            uint U3 = BR.ReadUInt32();
                            BW.Write(U3);
                            // Debug.Assert((U3 == 11), "U3 is bad");

                            uint U4 = BR.ReadUInt32();
                            BW.Write(U4);
                            // Debug.Assert(((U4 == 0x20000000) || (U4 == 0x60000000) || (U4 == 0x80000000)), "U4 is bad");

                            if (Test_PrintDebugInfo)
                                Debug.Print("        Animatable unknowns = {0} {1} {2} {3}", U1, U2, U3, U4);

                            // ToDo: If any of the asserts above fired, then we've lost control.
                            // The most likely explanation is that the previous entry's iExcessLen is wrong
                            // and we've read off the end of the previous entry.
                            // For now, the safest course of action is to stop processing these entries.
                            if (((U1 != 1) && (U1 != 2))
                             || ((U2 != 1) && (U2 != 0x09D0CE69))
                             || (U3 != 11)
                                //                       || ((U4 != 0x20000000) && (U4 == 0x60000000) && (U4 == 0x80000000))
                               )
                            {
                                Debug.Fail("Invalid entry: incorrect size; skip rest of entries");
                                return;
                            }

                            string S1 = LETools.Copy7BitStr(BR, BW, true);
                            if (Test_PrintDebugInfo)
                                Debug.Print("        Animatable S1 = {0}", S1);
                            string S2 = LETools.Copy7BitStr(BR, BW, true);
                            if (Test_PrintDebugInfo)
                                Debug.Print("        Animatable S2 = {0}", S2);
                            Debug.Flush();

                            int jCoordFlagIndex = 24;   // Location of coordinate flag
                            uint uCoordFlag = 0;        // Coordinate flag: 0xFFFFFFFF => Change coordinates at jCoordIndex
                            int jCoordIndex = 62;       // Location of coordinates
                            int jSizeFlagIndex = 28;    // Location of flag for reduced entry size

                            // Looks like DWORDS:
                            //     unknown unknown unknown unknown unknown unknown unknown unknown unknown unknown
                            //   0 int     int     int             float   float   float   int     int     guid?
                            //  40                                                         float   int     int
                            //  80 guid                            byte    int     guid            guid    int
                            // 117 guid    int     int             guid/i  guid/i  guid/f  guid/f  int     int
                            // 157 guid    int     guid    int     int             int             guid    float
                            // 197 guid    int     float   float   guid    int     float   guid    guid           
                            // 237 guid    float           float   float   float   float   float   float   float
                            // 277 float                                                   float   float          
                            // 317                                         float   float   int     int            
                            // 357 guid    guid    int     float          

                            // Handle excess data up to the extra set of coordinates
                            for (int j = 0; j < jCoordIndex; j++)
                            {
                                if (j == jCoordFlagIndex)
                                {
                                    // Just guessing as to where the extra byte is in the array.
                                    byte B = BR.ReadByte();
                                    BW.Write(B);
                                    if (Test_PrintDebugInfo)
                                        Debug.Print("        Animatable B = {0}", B);
                                    Debug.Print("{0,4}  {1:X2}", j * 4, B);
                                }
                                // Unknown data
                                uint U = BR.ReadUInt32();
                                BW.Write(U);
                                if (Test_PrintDebugInfo)
                                    Debug.Print("        Animatable U[{0}] = {1:X8}", j, U);
                                if (j == jCoordFlagIndex)
                                    uCoordFlag = U;
                                else if (j == jSizeFlagIndex)
                                {
                                    Debug.Assert((U == 1) || (U == 2) || (U == 5));
                                    if (U == 1)
                                        bReducedSize = true;    // Entries have reduced size
                                }

                                // Print possible data types to try to determine when iExcessLen--
                                byte[] bArray = new byte[4];
                                BinaryWriter BWT = new BinaryWriter(new MemoryStream(bArray));
                                BWT.Write(U);
#if ADJUST
                                LETools.PrintDataType(bArray, j * 4 + ((j < jCoordFlagIndex) ? 0 : 1));
#endif
                            }
                            // Handle the extra set of coordinates
                            // if (uCoordFlag == 0xFFFFFFFF)
                            {
                                if (HandleEntry(BR, BW, true, Coordinate_Animate4, "    Animatable Entry 4", "", bPortal))
                                {
                                    PrintCoordinate(Coordinate_Main, "    Main Coordinate:      ");
                                    Debug.Print("*** Coordinate changed! ***");
                                }
                                jCoordIndex += 7;
                            }
                            // Handle excess data after the extra set of coordinates
                            int iExcessLen = (U1 == 2) ? 96 : 94;   // Number of additional DWORD entries to process
                            if (bReducedSize)
                                iExcessLen--;
                            else if ((U1 == 2) && (U2 == 0x09D0CE69) && (U3 == 11) && (U4 == 0x80000000))
                                iExcessLen--;
                            for (int j = jCoordIndex; j < iExcessLen; j++)
                            {
                                // Unknown data
                                uint U = BR.ReadUInt32();
                                BW.Write(U);
                                if (Test_PrintDebugInfo)
                                    Debug.Print("        Animatable U[{0}] = {1:X8}", j, U);

                                // Print possible data types to try to determine when iExcessLen--
                                byte[] bArray = new byte[4];
                                BinaryWriter BWT = new BinaryWriter(new MemoryStream(bArray));
                                BWT.Write(U);
#if ADJUST
                                LETools.PrintDataType(bArray, j * 4 + 1);
#endif
                            }

                            string S3 = LETools.Copy7BitStr(BR, BW, true);
                            if (Test_PrintDebugInfo)
                                Debug.Print("        Animatable S3 = {0}", S3);

                            if (U1 == 2)
                            {
                                for (int j = 0; j < 11; j++)
                                {
                                    // Unknown data
                                    uint U = BR.ReadUInt32();
                                    BW.Write(U);
                                    if (Test_PrintDebugInfo)
                                        Debug.Print("        Animatable U[{0}] = {1:X8}", iExcessLen + j, U);
                                }
                            }

                            uint U97 = BR.ReadUInt32();
                            // Debug.Assert((U97 == 0) || (U97 == 1));
                            BW.Write(U97);
                            if (Test_PrintDebugInfo)
                                Debug.Print("        Animatable U97 = {0}", U97);
                            Debug.Print("*** Entry length *** {0}", iExcessLen * 4 + 1);
                            Debug.Flush();
                        }
                        Debug.Print("");
                    }
                    if (bPerson)
                    {
                        uint U98 = BR.ReadUInt32();
                        Debug.Assert((U98 == 0) || (U98 == 1));
                        BW.Write(U98);

                        uint U99 = BR.ReadUInt32();
                        Debug.Assert((U99 == 0) || (U99 == 1));
                        BW.Write(U99);

                        if (Test_PrintDebugInfo)
                            Debug.Print("        Animatable U98 = {0} U99 = {1}", U98, U99);
                        Debug.Flush();
                    }
                }
            }
            if (Test_PrintExcess)
            {
                // There should be no bytes left to process, but check anyway...
                int iAdditional = 0;
                for (; ; iAdditional++)
                {
                    try
                    {
                        BR.ReadByte();
                    }
                    catch
                    {
                        break;
                    }
                }
                if (iAdditional == 0)
                { }
                else if (bPerson)
                    Debug.Print("*** Person version {0} / {1} / {2} / {3} has {4} unexpected bytes", iPersonVersion, iLocomotableVersion, iAnimatableVersion, iObjectVersion, iAdditional);
                else
                    Debug.Print("*** Animatable version {0} / {1} has {2} unexpected bytes", iAnimatableVersion, iObjectVersion, iAdditional);
            }
        }

        private bool HandleEntry(BinaryReader BR, BinaryWriter BW,
            bool bChange, int i, string sEntry, string sName, bool bPortal)
        {
            bool bChanged = false;

            float XOld = BR.ReadSingle();
            float XNew = XOld;
#if ADJUST
            if (bChange && !bOnlyElevation)
            {
                if ((XOld > 0) || bPortal)
                {
                    // Check whether coordinates match main coordinates
                    // ToDo: What does it mean if they don't match?
                    if ((i > Coordinate_Main) && (i < Coordinate_Max))
                        if ((Coord[Coordinate_Main].Y != 0) || (Coord[Coordinate_Main].X != 0))
                            if (!LETools.FloatEqual(XOld, Coord[Coordinate_Main].X))
                                if (i != Coordinate_Animate3)
                                {
                                    // Debug.Fail("Unexpected X"); // ToDo: What now?  Why is Animate3 different?
                                }
                    XNew = LETools.KeepOnLot(XOld, XAddLow, XAddHigh, WidthNew, 0.5F);
                    if (XOld != XNew)
                        bChanged = true;
                }
            }
#endif

            float YOld = BR.ReadSingle();
            float YNew = YOld;
#if ADJUST
            if (bChange && !bOnlyElevation)
            {
                if ((YOld > 0) || bPortal)
                {
                    // Check whether coordinates match main coordinates
                    // ToDo: What does it mean if they don't match?
                    if ((i > Coordinate_Main) && (i < Coordinate_Max))
                        if ((Coord[Coordinate_Main].Y != 0) || (Coord[Coordinate_Main].X != 0))
                            if (!LETools.FloatEqual(YOld, Coord[Coordinate_Main].Y))
                                if (i != Coordinate_Animate3)
                                {
                                    // Debug.Fail("Unexpected Y"); // ToDo: What now?  Why is Animate3 different?
                                }
                    YNew = LETools.KeepOnLot(YOld, YAddLow, YAddHigh, HeightNew, 0.5F);
                    if (YOld != YNew)
                        bChanged = true;
                }
            }
#endif

            float ZOld = BR.ReadSingle();  // Z = Height in meters
            float ZNew = ZOld;
#if ADJUST
            if (bChange && bOnlyElevation)
            {
                ZNew = fElevation;
                if (ZOld != ZNew)
                    bChanged = true;
            }
#else
            if (bChange && bOnlyElevation && (ZOld >= fMinimum))
            {
                if ((i == Coordinate_Main) && (0F == XOld) && (0F == YOld) && (0F == ZOld))
                {
                    Debug.Print("Found one!");
                    fElevation = 0.0F;
                }
                else
                {
                    ZNew = ZOld + fElevation;
                    bChanged = true;
                }
            }
#endif

            UInt32 rA = BR.ReadUInt32();
            UInt32 rB = BR.ReadUInt32();
            UInt32 rC = BR.ReadUInt32();
            UInt32 rD = BR.ReadUInt32();

            // Find original rotation
            int rOrig = 0;
            for (; rOrig < Direction_None; rOrig++)
            {
                if ((rA == Rotation[rOrig].qA)
                 && (rB == Rotation[rOrig].qB)
                 && (rC == Rotation[rOrig].qC)
                 && (rD == Rotation[rOrig].qD))
                    break;
            }

            if ((i >= 0) && (i < Coordinate_Max))
                Coord[i] = new Coordinate(XOld, YOld, ZOld, rA, rB, rC, rD);

            if (bChange && !bOnlyElevation)
            {
                if (bPortal && (Direction != Direction_None) /* && (rOrig != Direction_None) */)
                {
                    if (Test_AssertDirection)
                        Debug.Assert(rOrig == Direction);
                    rA = Rotation[Direction].qA;
                    rB = Rotation[Direction].qB;
                    rC = Rotation[Direction].qC;
                    rD = Rotation[Direction].qD;
                }
            }

            BW.Write(XNew);
            BW.Write(YNew);
            BW.Write(ZNew);
            BW.Write(rA);
            BW.Write(rB);
            BW.Write(rC);
            BW.Write(rD);

            if (i == Coordinate_Main)
                Coord[Coordinate_New] = new Coordinate(XNew, YNew, ZNew, rA, rB, rC, rD);

            if (Test_PrintDebugInfo)
            {
                if (bChange)
                {
                    if (bChanged)
                        Debug.Print("{0}: \"{1}\" X={2}->{3} Y={4}->{5} Z={6}->{7} Rotation={8:X8} {9:X8} {10:X8} {11:X8} {12}",
                            sEntry, sName, XOld, XNew, YOld, YNew, ZOld, ZNew, rA, rB, rC, rD, Rotation[rOrig].Desc);
                    else
                        Debug.Print("{0}: \"{1}\" X={2} Y={3} Z={4} Rotation={5:X8} {6:X8} {7:X8} {8:X8} {9}",
                            sEntry, sName, XOld, YOld, ZOld, rA, rB, rC, rD, Rotation[rOrig].Desc);
                }
            }
            return bChanged;
        }

        public void PrintDebugInfo(bool b)
        {
            Test_PrintDebugInfo = b;
        }

        public void PrintData()
        {
#if ADJUST
            Test_PrintDebugInfo = true;

            // Problem with deadlock?
            LETools.PrintData(Data);
#endif
        }

        private void PrintCoordinate(int i, string desc)
        {
/*          byte[] bArray = new byte[7 * sizeof(float)];

            BinaryWriter BW = new BinaryWriter(new MemoryStream(bArray));
            BW.Write(Coord[i].X);
            BW.Write(Coord[i].Y);
            BW.Write(Coord[i].Z);
            BW.Write(Coord[i].Rot.qA);
            BW.Write(Coord[i].Rot.qB);
            BW.Write(Coord[i].Rot.qC);
            BW.Write(Coord[i].Rot.qD);
            LETools.PrintData(bArray);
 */
            Debug.Print("{0} X={1} Y={2} Z={3} Rotation={4:X8} {5:X8} {6:X8} {7:X8}",
                desc, Coord[i].X, Coord[i].Y, Coord[i].Z,
                Coord[i].Rot.qA, Coord[i].Rot.qB, Coord[i].Rot.qC, Coord[i].Rot.qD);
        }
    }
}
