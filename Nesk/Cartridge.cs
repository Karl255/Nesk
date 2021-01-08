using System;
using Nesk.Shared;

namespace Nesk
{
	public class Cartridge
	{
		public byte[] OriginalFileData { get; init; }

		public bool HasTrainer { get; private set; }
		public byte[] Trainer { get; private set; }

		public byte[] PrgRom { get; private set; }
		public int PrgRomSize { get; private set; } = 0;

		public byte[] ChrRom { get; private set; }
		public int ChrRomSize { get; private set; } = 0;

		// misc. ROM area, only used in few cases, mappers will further handle this (if they even get implemented)
		public byte[] TrailingData { get; private set; }
		public int MiscROMCount { get; private set; }

		public bool HasPrgRam { get; private set; }
		public int PrgRamSize { get; private set; }
		public int PrgNvramSize { get; private set; }
		public int ChrRamSize { get; private set; }
		public int ChrNvramSize { get; private set; }

		public int Mapper { get; private set; }
		public int Submapper { get; private set; }

		public MirroringMode NametableMirroring { get; private set; }
		public ConsoleType ConsoleType { get; private set; }
		public TimingMode TimingMode { get; private set; }
		public bool HasBusConflicts { get; private set; }

		public VsPPUType VsPPUType { get; private set; }
		public VsHardwareType VsHardwareType { get; private set; }
		public ExpansinDevice DefaultExpansionDevice { get; private set; }

		/// <summary>
		/// Creates a new <see cref="Cartridge"/> object from the specified ROM file contents.
		/// </summary>
		/// <param name="fileData">The contents of the ROM file.</param>
		public Cartridge(byte[] fileData)
		{
			OriginalFileData = fileData;

			string first4B = System.Text.Encoding.ASCII.GetString(fileData, 0, 4);

			if (first4B == "NES\x1a")
			{
				if ((fileData[7] & 0x0c) == 0x08)
					// NES 2.0
					ParseNES2Only(fileData);

				else if ((fileData[7] & 0x0c) == 0x00
					&& fileData[12] == 0
					&& fileData[13] == 0
					&& fileData[14] == 0
					&& fileData[15] == 0)
					// iNES
					ParseiNESOnly(fileData);

				else
					// non-standard or archaic iNES (not supported)
					throw new Exception("Malformed iNES fileData or archaic format");

				ParseNESCommon(fileData);
			}
			else if (first4B == "UNIF")
			{
				// TODO: implement UNIF
				throw new NotImplementedException("Not implemented yet: UNIF file format");
			}
			else
				throw new Exception("Unsupported format or not a ROM file");
		}

		private void ParseNES2Only(byte[] fileData)
		{
			// http://wiki.nesdev.com/w/index.php/NES_2.0

			// nibble 2 of mapper number (8..11)
			Mapper = (fileData[8] & 0x0f) << 8;
			Submapper = (fileData[8] & 0xf0) >> 4;

			PrgRomSize = (fileData[9] & 0x0f) << 8;
			ChrRomSize = (fileData[9] & 0xf0) << 4;

			// 0 => not present instead of no shift
			int t = fileData[10] & 0x0f;
			PrgRamSize = t == 0 ? 0 : 64 << t;

			// 0 => not present instead of no shift
			t = fileData[10] & 0xf0;
			PrgNvramSize = t == 0 ? 0 : 64 << t;

			// 0 => not present instead of no shift
			t = fileData[11] & 0x0f;
			ChrRamSize = t == 0 ? 0 : 64 << t;

			// 0 => not present instead of no shift
			t = fileData[11] & 0xf0;
			ChrNvramSize = t == 0 ? 0 : 64 << t;

			TimingMode = (TimingMode)(fileData[12] & 0x3);
			ConsoleType = (ConsoleType)((fileData[7] & 0x3) < 3
				? fileData[7] & 0x3
				: fileData[13] & 0xf
			);

			if (ConsoleType == ConsoleType.VsSystem)
			{
				VsPPUType = (VsPPUType)(fileData[13] & 0xf);
				VsHardwareType = (VsHardwareType)((fileData[13] & 0xf0) >> 4);
			}

			MiscROMCount = fileData[14] & 2;
			DefaultExpansionDevice = (ExpansinDevice)(fileData[15] & 0x3f);
		}

		private void ParseiNESOnly(byte[] fileData)
		{
			// http://wiki.nesdev.com/w/index.php/INES

			// size in 8 kB chunks (0 still means 1 for compatibility)
			PrgRamSize = (fileData[8] == 0 ? 1 : fileData[8]) * 8 * 1024;

			TimingMode = (TimingMode)(fileData[0] & 1);
			ConsoleType = (ConsoleType)(fileData[7] & 0x3);
		}

		private void ParseNESCommon(byte[] fileData)
		{
			// common for both NES 2.0 and iNES

			PrgRomSize |= fileData[4];
			ChrRomSize |= fileData[5];

			// if ROM size is 1111 xxxx xxxx
			if ((PrgRomSize & 0xf00) == 0xf00)
				// format: 1111 EEEE EEMM
				// final value: = 2^E * (M * 2 + 1) B
				PrgRomSize = (int)Math.Pow(2, (PrgRomSize & 0x0fc) >> 2) * (PrgRomSize * 2 + 1);
			else
				// final value is in 16k units (gets converted to B)
				PrgRomSize *= 16 * 1024;

			// if ROM size is 1111 xxxx xxxx
			if ((ChrRomSize & 0xf00) == 0xf00)
				// format: 1111 EEEE EEMM
				// final value: = 2^E * (M * 2 + 1) B
				ChrRomSize = (int)Math.Pow(2, (ChrRomSize & 0x0fc) >> 2) * (ChrRomSize * 2 + 1);
			else
				// final value is in 8k units (gets converted to B)
				ChrRomSize *= 8 * 1024;

			NametableMirroring = (fileData[6].GetBit(3), fileData[6].GetBit(1)) switch
			{
				(0, 0) => MirroringMode.Horizontal,
				(0, 1) => MirroringMode.Vertical,
				(1, _) => MirroringMode.FourScreen,
				_ => throw new Exception()
			};

			HasPrgRam = fileData[6].GetBit(1) == 1;
			HasTrainer = fileData[6].GetBit(2) == 1;

			Mapper |= (fileData[6] & 0xf0) | ((fileData[7] & 0xf0) >> 4);

			// data after the header

			int dataStart = 16;

			if (HasTrainer)
			{
				Trainer = new byte[512];
				Array.Copy(fileData, dataStart, Trainer, 0, 512);
				dataStart += 512;
			}

			int size = PrgRomSize;
			PrgRom = new byte[size];
			Array.Copy(fileData, dataStart, PrgRom, 0, size);
			dataStart += size;

			size = ChrRomSize;
			ChrRom = new byte[size];
			Array.Copy(fileData, dataStart, ChrRom, 0, size);
			dataStart += size;

			// check for trailing data
			int trailingLength = fileData.Length - dataStart;
			if (trailingLength > 0)
			{
				TrailingData = new byte[trailingLength];
				Array.Copy(fileData, dataStart, TrailingData, 0, trailingLength);
				dataStart += trailingLength;
			}
		}
	}
}
