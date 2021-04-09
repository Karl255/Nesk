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

		private byte ScrollX = 0x00;
		private byte ScrollY = 0x00;

		private bool AddressScrollLatch = true;
		private DoubleRegister Address = new(0x0000);
		private byte DataBuffer = 0x00;

		private byte OamAddress = 0x00;
		public bool IsOamDma { get; private set; } = false;
		private DoubleRegister OamDmaAddress = new(0);
		private bool IsOamDmaReading = false;

		public Action NmiRaiser { private get; set; } = null;

		private int Cycle = 0;
		private int Scanline = 0;
		public bool IsFrameReady { get; private set; }

		private byte[,] InterBuffer { get; init; } = new byte[256, 240];
		private static readonly byte[][] BlankBuffer = new byte[][]
		{
			Resources.BlankBitmap1x.CloneArray(),
			Resources.BlankBitmap2x.CloneArray(),
			Resources.BlankBitmap3x.CloneArray(),
			Resources.BlankBitmap4x.CloneArray()
		};

		public int AddressableSize => 8;
		public bool IsReadonly { get; set; }

		private static readonly ImmutableArray<(byte R, byte G, byte B)> ColorPalette = ImmutableArray.Create(new (byte, byte, byte)[]
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
						returnedData = Oam[OamAddress];
						break;

					case 7: // data
							// read "The PPUDATA read buffer (post-fetch)" http://wiki.nesdev.com/w/index.php/PPU_registers#PPUDATA
						if (Address.Whole < 0x3f00)
							returnedData = DataBuffer;
						else
							returnedData = Memory[address];

						DataBuffer = Memory[Address.Whole];
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
						// if vblank and GenerateNmi goes from off (low) to on (high)
						if (Status.VerticalBlank && !Control.GenerateNmi && (value & 0x80) != 0)
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
						Oam[OamAddress] = value;
						break;

					case 5: // scroll
							// first x scroll then y scroll (initial value of the latch is 1)
						if (AddressScrollLatch)
							ScrollX = value;
						else
							ScrollY = value;

						AddressScrollLatch = !AddressScrollLatch;
						break;

					case 6: // address
							// first high byte then low byte (initial value of the latch is 1)
						if (AddressScrollLatch)
							Address.Upper = value;
						else
							Address.Lower = value;

						AddressScrollLatch = !AddressScrollLatch;
						break;

					case 7: // data
						Memory[Address.Whole] = value;
						Address.Whole += (ushort)(Control.IncrementMode ? 32 : 1);
						break;

					case 0x14: // OAM DMA
						OamDmaAddress.Upper = value;
						IsOamDma = true;
						break;

					default:
						break;
				}
			}
		}

		// NOTE & TODO: this takes 512 CPU cycles instead of 513/514; imeplement correct timimng
		public void DoOamDma(Mappers.CpuMapper cpuBus)
		{
			if (IsOamDmaReading)
			{
				Oam[OamDmaAddress.Lower] = cpuBus[OamDmaAddress.Whole];
				OamDmaAddress.Lower++;

				if (OamDmaAddress.Lower == 0xff)
					IsOamDma = false;
			}

			IsOamDmaReading = !IsOamDmaReading;
		}

		public void Tick()
		{
			// TODO: implement proper ticking

			Cycle++;

			if (Cycle > 341)
			{
				Cycle = 0;
				Scanline++;

				if (Scanline > 261)
				{
					Scanline = -1;
					IsFrameReady = true;
				}
			}

			// vblank start
			if (Scanline == 241 && Cycle == 1)
			{
				Status.VerticalBlank = true;
				Status.Sprite0Hit = false;
				if (Control.GenerateNmi)
					NmiRaiser?.Invoke();
			}

			// vblank end
			if (Scanline == 261 && Cycle == 1)
				Status.VerticalBlank = false;
		}

		public static byte[] RenderInterFrame(byte[,] interFrame, int scale)
		{
			if (scale is < 1 or > 4)
				throw new Exception($"Invalid frame scale ({scale}).");

			byte[] buffer = BlankBuffer[scale - 1];
			int start = buffer[0x0A];

			for (int y = 0; y < 240 * scale; y++)
			{
				for (int x = 0; x < 256 * scale; x++)
				{
					int fixedY = 240 * scale - 1 - y;
					var color = ColorPalette[interFrame[x / scale, y / scale] & 0x3f];
					buffer[start + (fixedY * 256 * scale + x) * 3 + 0] = color.B; // B
					buffer[start + (fixedY * 256 * scale + x) * 3 + 1] = color.G; // G
					buffer[start + (fixedY * 256 * scale + x) * 3 + 2] = color.R; // R
				}
			}

			return buffer;
		}

		public byte[,] GetFrame()
		{
			if (IsFrameReady)
			{
				IsFrameReady = false;

				for (int nametableY = 0; nametableY < 30; nametableY++)
				{
					for (int nametableX = 0; nametableX < 32; nametableX++)
					{
						int patternId = Memory[0x2000 + 32 * nametableY + nametableX + (Control.BackgroundAddress ? 0x1000 : 0)];

						int palette = Memory[0x23c0 + nametableY / 4 * 8 + nametableX / 4] >> (nametableY / 2 % 2 * 2 + nametableX % 2);
						var color0 = Memory[0x3f00];
						var color1 = Memory[0x3f00 + palette * 4 + 1];
						var color2 = Memory[0x3f00 + palette * 4 + 2];
						var color3 = Memory[0x3f00 + palette * 4 + 3];

						for (int spriteY = 0; spriteY < 8; spriteY++)
						{
							int rowByteLower = Memory[patternId * 16 + spriteY];
							int rowByteUpper = Memory[patternId * 16 + spriteY + 0x0008];

							for (int spriteX = 0; spriteX < 8; spriteX++)
							{
								int totalX = nametableX * 8 + spriteX;
								int totalY = nametableY * 8 + spriteY;

								int pixelColor = ((rowByteLower >> (7 - spriteX)) & 1)
									| (((rowByteUpper >> (7 - spriteX)) & 1) << 1);

								InterBuffer[totalX, totalY] = pixelColor switch
								{
									0 => color0,
									1 => color1,
									2 => color2,
									3 => color3,
									_ => 0
								};
							}
						}

					}
				}

				return InterBuffer;
			}
			else
				return null;
		}

		public byte[,] GetPatternMemoryAsFrame(int palette)
		{
			byte[] colors =
			{
				(byte)(palette >= 0 ? Memory[0x3f00]                   : 0x3f),
				(byte)(palette >= 0 ? Memory[0x3f00 + palette * 4 + 1] : 0x27),
				(byte)(palette >= 0 ? Memory[0x3f00 + palette * 4 + 2] : 0x36),
				(byte)(palette >= 0 ? Memory[0x3f00 + palette * 4 + 3] : 0x11)
			};

			for (int sprite = 0; sprite < 256; sprite++)
			{
				for (int spriteY = 0; spriteY < 8; spriteY++)
				{
					int rowByteLower = Memory[sprite * 16 + spriteY];
					int rowByteUpper = Memory[sprite * 16 + spriteY + 0x0008];

					for (int spriteX = 0; spriteX < 8; spriteX++)
					{
						int x = sprite % 16 * 8 + spriteX;
						int y = sprite / 16 * 8 + spriteY;
						int pixelColor = ((rowByteLower >> (7 - spriteX)) & 1)
							| (((rowByteUpper >> (7 - spriteX)) & 1) << 1);

						InterBuffer[x, y] = colors[pixelColor];
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
						int y = sprite / 16 * 8 + spriteY;
						int pixelColor = ((rowByteLower >> (7 - spriteX)) & 1)
							| (((rowByteUpper >> (7 - spriteX)) & 1) << 1);

						InterBuffer[x, y] = colors[pixelColor];
					}
				}
			}

			for (int y = 128; y < 240; y++)
			{
				for (int x = 0; x < 256; x++)
				{
					InterBuffer[x, y] = 0x3f;
				}
			}

			return InterBuffer;
		}

		public byte[,] GetNametableAsFrame(int nametable)
		{
			int nametableStart = nametable switch
			{
				0 => 0x2000,
				1 => 0x2400,
				2 => 0x2800,
				3 => 0x2c00,
				_ => 0x2000
			};

			for (int nametableY = 0; nametableY < 30; nametableY++)
			{
				for (int nametableX = 0; nametableX < 32; nametableX++)
				{
					int patternId = Memory[nametableStart + 32 * nametableY + nametableX + (Control.BackgroundAddress ? 0x1000 : 0)];

					int palette = Memory[nametableStart + 0x03c0 + nametableY / 4 * 8 + nametableX / 4] >> (nametableY / 2 % 2 * 2 + nametableX % 2);
					var color0 = Memory[0x3f00];
					var color1 = Memory[0x3f00 + palette * 4 + 1];
					var color2 = Memory[0x3f00 + palette * 4 + 2];
					var color3 = Memory[0x3f00 + palette * 4 + 3];

					for (int spriteY = 0; spriteY < 8; spriteY++)
					{
						int rowByteLower = Memory[patternId * 16 + spriteY];
						int rowByteUpper = Memory[patternId * 16 + spriteY + 0x0008];

						for (int spriteX = 0; spriteX < 8; spriteX++)
						{
							int totalX = nametableX * 8 + spriteX;
							int totalY = nametableY * 8 + spriteY;

							int pixelColor = ((rowByteLower >> (7 - spriteX)) & 1)
									| (((rowByteUpper >> (7 - spriteX)) & 1) << 1);

							InterBuffer[totalX, totalY] = pixelColor switch
							{
								0 => color0,
								1 => color1,
								2 => color2,
								3 => color3,
								_ => 0
							};
						}
					}

				}
			}

			return InterBuffer;
		}
	}
}
