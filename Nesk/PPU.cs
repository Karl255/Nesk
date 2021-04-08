using System;
using System.Collections.Immutable;
using K6502Emu;
using Nesk.Shared;

namespace Nesk
{
	public class Ppu : IAddressable<byte>
	{
		private IAddressable<byte> Memory { get; init; }

		private byte[] Oam { get; init; } = new byte[256];

		private PpuControlRegister Control = new(0x00);
		private PpuMaskRegister    Mask    = new(0x00);
		private PpuStatusRegister  Status  = new(0b1010_0000);

		private byte OamAddress = 0x00;
		private byte OamData = 0x00;

		private byte ScrollX = 0x00;
		private byte ScrollY = 0x00;

		private bool AddressScrollLatch = true;
		private int Address = 0x0000;
		private byte DataBuffer = 0x00;

		public bool IsOamDma { get; private set; } = false;
		private ushort OamDmaAddress = 0;
		private bool IsOamDmaReading = false;

		private readonly byte[] BlankBuffer = Shared.Resources.BlankBitmap.CloneArray();
		private int Cycle = 0;
		private int Scanline = 0;
		public bool IsFrameReady { get; private set; }

		public int AddressableSize => 8;
		public bool IsReadonly { get; set; }

		public Action NmiRaiser { private get; set; } = null;

		private ImmutableArray<(byte, byte, byte)> ColorPalette { get; init; } = ImmutableArray.Create(new (byte, byte, byte)[]
		{
			(84,  84,  84),
			(0,   30,  116),
			(8,   16,  144),
			(48,  0,   136),
			(68,  0,   100),
			(92,  0,   48),
			(84,  4,   0),
			(60,  24,  0),
			(32,  42,  0),
			(8,   58,  0),
			(0,   64,  0),
			(0,   60,  0),
			(0,   50,  60),
			(0,   0,   0),
			(0,   0,   0),
			(0,   0,   0),
			(152, 150, 152),
			(8,   76,  196),
			(48,  50,  236),
			(92,  30,  228),
			(136, 20,  176),
			(160, 20,  100),
			(152, 34,  32),
			(120, 60,  0),
			(84,  90,  0),
			(40,  114, 0),
			(8,   124, 0),
			(0,   118, 40),
			(0,   102, 120),
			(0,   0,   0),
			(0,   0,   0),
			(0,   0,   0),
			(236, 238, 236),
			(76,  154, 236),
			(120, 124, 236),
			(176, 98,  236),
			(228, 84,  236),
			(236, 88,  180),
			(236, 106, 100),
			(212, 136, 32),
			(160, 170, 0),
			(116, 196, 0),
			(76,  208, 32),
			(56,  204, 108),
			(56,  180, 204),
			(60,  60,  60),
			(0,   0,   0),
			(0,   0,   0),
			(236, 238, 236),
			(168, 204, 236),
			(188, 188, 236),
			(212, 178, 236),
			(236, 174, 236),
			(236, 174, 212),
			(236, 180, 176),
			(228, 196, 144),
			(204, 210, 120),
			(180, 222, 120),
			(168, 226, 144),
			(152, 226, 180),
			(160, 214, 228),
			(160, 162, 160),
			(0,   0,   0),
			(0,   0,   0)
		});

		public Ppu(IAddressable<byte> memoryMapper) => Memory = memoryMapper;

		public byte this[int address]
		{
			get
			{
				byte returnedData = 0;
				switch (address)
				{
					case 2: // status
							// top 3 bytes exist, the bottom 5 don't, so static noise determines them
						returnedData = (byte)(Status.Byte | (DataBuffer & 0x1f));
						Status.VerticalBlank = false;
						AddressScrollLatch = true; // reading from status resets the write latch for those 2 registers
						break;

					case 4: // OAM Data
						returnedData = OamData;
						break;

					case 7: // data
							// reading is delayed by 1 cycle (except for palette memory)
						if (Address < 0x3f00)
						{
							returnedData = DataBuffer;
							DataBuffer = Memory[Address];
						}
						else
							returnedData = DataBuffer = Memory[address];
						break;

					default:
						break;
				}

				return returnedData;
			}

			set
			{
				switch (address)
				{
					case 0: // control
						if (Status.VerticalBlank && !Control.GenerateNMI && (value & 0x80) != 0)
							NmiRaiser?.Invoke();

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
							// first high byte then low byte (initial value of the latch is 1)
						if (AddressScrollLatch)
							Address = (Address & 0xff) | (value << 8);
						else
							Address = (Address & 0xff00) | value;

						AddressScrollLatch = !AddressScrollLatch;
						break;

					case 7: // data
						Memory[Address] = DataBuffer = value;
						Address += Control.IncrementMode ? 32 : 1;
						break;

					case 0x14: // OAM DMA
						OamDmaAddress = (ushort)(value << 8);
						IsOamDma = true;
						break;

					default:
						break;
				}
			}
		}

		// NOTE & TODO: 
		public void DoOamDma(Mappers.CpuMapper cpuBus)
		{
			if (IsOamDmaReading)
			{
				Oam[(byte)OamDmaAddress] = cpuBus[OamDmaAddress];
				OamDmaAddress++;

				if (0xff == (byte)OamDmaAddress)
				{
					IsOamDma = false;
				}
			}

			IsOamDmaReading = !IsOamDmaReading;
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

			if (Scanline == 241 && Cycle == 1)
			{
				Status.VerticalBlank = true;
				if (Control.GenerateNMI)
					NmiRaiser?.Invoke();
			}

			if (Scanline == 261 && Cycle == 1)
				Status.VerticalBlank = false;
		}

		public byte[] GetFrame()
		{
			if (IsFrameReady)
			{
				IsFrameReady = false;

				byte[] frameBuffer = BlankBuffer.CloneArray();
				int start = frameBuffer[0x0A];

				for (int nametableY = 0; nametableY < 30; nametableY++)
				{
					for (int nametableX = 0; nametableX < 32; nametableX++)
					{
						int patternId = Memory[0x2000 + 32 * nametableY + nametableX + (Control.BackgroundAddress ? 0x1000 : 0)];

						int palette = Memory[0x23c0 + nametableY / 4 * 8 + nametableX / 4] >> (nametableY / 2 % 2 * 2 + nametableX % 2);
						var color0 = ColorPalette[Memory[0x3f00]];
						var color1 = ColorPalette[Memory[0x3f00 + palette * 4 + 1]];
						var color2 = ColorPalette[Memory[0x3f00 + palette * 4 + 2]];
						var color3 = ColorPalette[Memory[0x3f00 + palette * 4 + 3]];

						for (int spriteY = 0; spriteY < 8; spriteY++)
						{
							int rowByteLower = Memory[patternId * 16 + spriteY];
							int rowByteUpper = Memory[patternId * 16 + spriteY + 0x0008];

							for (int spriteX = 0; spriteX < 8; spriteX++)
							{
								int totalX = nametableX * 8 + spriteX;
								int totalY = 239 - (nametableY * 8 + spriteY);

								int pixelColor = ((rowByteLower >> (7 - spriteX)) & 1)
									| (((rowByteUpper >> (7 - spriteX)) & 1) << 1);

								(
									frameBuffer[start + (totalY * 256 + totalX) * 3 + 2], // R
									frameBuffer[start + (totalY * 256 + totalX) * 3 + 1], // G
									frameBuffer[start + (totalY * 256 + totalX) * 3 + 0]  // B
								) = pixelColor switch
								{
									0 => color0,
									1 => color1,
									2 => color2,
									3 => color3,
									_ => ((byte)0, (byte)0, (byte)0)
								};
							}
						}

					}
				}

				return frameBuffer;
			}
			else
				return null;

			/*
			if (IsFrameReady)
			{
				IsFrameReady = false;

				// generate greyscale noise and return that as the frame
				byte[] frameBuffer = BlankBuffer.CloneArray();
				int start = frameBuffer[0x0A];
				Random rand = new();

				for (int y = 0; y < 240; y++)
				{
					for (int x = 0; x < 256;
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
			*/
		}

		public byte[] RenderPatternMemory(int palette)
		{
			var color0 = palette >= 0 ? ColorPalette[Memory[0x3f00]]                   : ((byte)0  , (byte)0  , (byte)0  );
			var color1 = palette >= 0 ? ColorPalette[Memory[0x3f00 + palette * 4 + 1]] : ((byte)255, (byte)0  , (byte)0  );
			var color2 = palette >= 0 ? ColorPalette[Memory[0x3f00 + palette * 4 + 2]] : ((byte)0  , (byte)255, (byte)0  );
			var color3 = palette >= 0 ? ColorPalette[Memory[0x3f00 + palette * 4 + 3]] : ((byte)0  , (byte)0  , (byte)255);

			byte[] frameBuffer = BlankBuffer.CloneArray();
			int start = frameBuffer[0x0A];

			for (int sprite = 0; sprite < 256; sprite++)
			{
				for (int spriteY = 0; spriteY < 8; spriteY++)
				{
					int rowByteLower = Memory[sprite * 16 + spriteY];
					int rowByteUpper = Memory[sprite * 16 + spriteY + 0x0008];

					for (int spriteX = 0; spriteX < 8; spriteX++)
					{
						int x = sprite % 16 * 8 + spriteX;
						int y = 239 - (sprite / 16 * 8 + spriteY);
						int pixelColor = ((rowByteLower >> (7 - spriteX)) & 1)
							| (((rowByteUpper >> (7 - spriteX)) & 1) << 1);

						(
							frameBuffer[start + (y * 256 + x) * 3 + 2], // R
							frameBuffer[start + (y * 256 + x) * 3 + 1], // G
							frameBuffer[start + (y * 256 + x) * 3 + 0]  // B
						) = pixelColor switch
						{
							0 => color0,
							1 => color1,
							2 => color2,
							3 => color3,
							_ => ((byte)0, (byte)0, (byte)0)
						};
					}
				}
			}

			for (int sprite = 0; sprite < 256; sprite++)
			{
				for (int spriteY = 0; spriteY < 8; spriteY++)
				{
					int rowByteLower = Memory[sprite * 16 + spriteY + 0x1000];
					int rowByteUpper = Memory[sprite * 16 + spriteY + 0x1008];

					for (int spriteX = 0; spriteX < 8; spriteX++)
					{
						int x = sprite % 16 * 8 + spriteX + 128;
						int y = 239 - (sprite / 16 * 8 + spriteY);
						int pixelColor = ((rowByteLower >> (7 - spriteX)) & 1)
							| (((rowByteUpper >> (7 - spriteX)) & 1) << 1);

						(
							frameBuffer[start + (y * 256 + x) * 3 + 2], // R
							frameBuffer[start + (y * 256 + x) * 3 + 1], // G
							frameBuffer[start + (y * 256 + x) * 3 + 0]  // B
						) = pixelColor switch
						{
							0 => color0,
							1 => color1,
							2 => color2,
							3 => color3,
							_ => ((byte)0, (byte)0, (byte)0)
						};
					}
				}
			}

			return frameBuffer;
		}
	}
}
