using System;
using K6502Emu;
using Nesk.Shared;

namespace Nesk
{
	public class PPU : IAddressable<byte>
	{
		private readonly IAddressable<byte> Memory;

		private PPUControlRegister Control = new(0x00);
		private PPUMaskRegister    Mask    = new(0x00);
		private PPUStatusRegister  Status  = new(0b1010_0000);

		private byte OamAddress = 0x00;
		private byte OamData = 0x00;

		private byte ScrollX = 0x00;
		private byte ScrollY = 0x00;

		private bool AddressScrollLatch = false;
		private int Address = 0x0000;
		private byte Data = 0x00;
		private byte OamDma = 0x00;

		private readonly byte[] BlankBuffer = Shared.Resources.BlankBitmap;
		private int Cycle = 0;
		private int Scanline = 0;
		public bool IsFrameReady { get; private set; }

		public int AddressableSize => 8;
		public bool IsReadonly { get; set; }

		public PPU(IAddressable<byte> memoryMapper)
		{
			Memory = memoryMapper;
		}

		public byte this[int address]
		{
			get
			{
				byte data = 0;
				switch (address)
				{
					case >= 0x2000 and <= 0x3fff:
						switch (address & 0x7)
						{
							case 2: // status
								data = (byte)((Data & 0x1f) | Status.Byte);
								break;

							case 4: // OAM Data
								data = OamData;
								break;

							case 7: // data
									// nametable reading is delayed by 1 cycle
								if (Address is >= 0x3f00 and <= 0x3fff)
								{
									data = Data;
									Data = Memory[Address];
								}
								else
									data = Data = Memory[address];
								break;

							default:
								break;
						}

						break;

					case 0x4014: // OAM DMA
						data = OamDma;
						break;

					default:
						break;
				}

				return data;
			}

			set
			{
				switch (address)
				{
					case >= 0x2000 and <= 0x3fff:
						switch (address & 0x7)
						{
							case 0: // control
								Control.Byte = value;
								break;

							case 1: // mask
								Mask.Byte = value;
								break;

							case 3: // OAM address
								OamAddress = value;
								break;

							case 4: // OAM data
								OamData = value;
								break;

							case 5: // scroll
								if (AddressScrollLatch)
									ScrollY = value;
								else
									ScrollX = value;

								AddressScrollLatch = !AddressScrollLatch;
								break;

							case 6: // address
								if (AddressScrollLatch)
									Address = (Address & 0xff) | (value << 8);
								else
									Address = (Address & 0xff00) | value;

								AddressScrollLatch = !AddressScrollLatch;
								break;

							case 7: // data
								Memory[Address] = Data = value;
								Address += Control.IncrementMode ? 32 : 1;
								break;
						}
						break;

					case 0x4014:
						OamDma = value;
						break;
				}
			}
		}

		public void Tick()
		{
			// TODO: implement proper ticking

			Cycle++;

			if (Cycle >= 341)
			{
				Cycle = 0;
				Scanline++;

				if (Scanline >= 261)
				{
					Scanline = -1;
					IsFrameReady = true;
				}
			}
		}

		public byte[] GetFrame()
		{
			if (IsFrameReady)
			{
				IsFrameReady = false;

				// generate greyscale noise and return that as the frame
				byte[] frameBuffer = BlankBuffer.CloneArray();
				int start = frameBuffer[0x0A];
				Random rand = new();

				for (int y = 0; y < 240; y++)
				{
					for (int x = 0; x < 256; x++)
					{
						byte v = (byte)rand.Next();
						frameBuffer[start + (y * 256 + x) * 3 + 0] = v; //B
						frameBuffer[start + (y * 256 + x) * 3 + 1] = v; //G
						frameBuffer[start + (y * 256 + x) * 3 + 2] = v; //R
					}
				}

				return frameBuffer;
			}
			else
				return null;
		}
	}
}
