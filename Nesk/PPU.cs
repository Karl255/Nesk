using System;
using System.Collections.Immutable;
using K6502Emu;
using Nesk.Shared;

namespace Nesk
{
	public class Ppu : IAddressable<byte>
	{
		private readonly IAddressable<byte> Memory;
		private readonly byte[] Oam = new byte[256]; // stores all the 64 sprites
		private readonly byte[] SecondaryOam = new byte[32]; //stores the 8 sprites for the scanline

		private PpuControlRegister Control = new(0x00);
		private PpuMaskRegister    Mask    = new(0x00);
		private PpuStatusRegister  Status  = new(0b1010_0000);
		private DoubleRegister AddressT = new(0x0000); // this is what you would usually refer to as the T register
		private int CurrentVramAddressV = 0;           // ... V register
		private bool RegisterWriteLatch = false;       // ... W latch
		private byte RegisterAccessNoise = 0x00;
		private byte DataBuffer = 0x00;
		private byte FineScrollX = 0;

		private DoubleRegister BgPatternShifterLower = new(0);
		private DoubleRegister BgPatternShifterUpper = new(0);
		private DoubleRegister BgAttributeShifter = new(0);    // combined 2 8-bit shifters into a single 16 bit shifter

		private readonly byte[] SpritePatternShifters = new byte[8];
		private readonly byte[] SpriteAttributeLatches = new byte[8];
		private readonly byte[] SpriteXPositionCounters = new byte[8];

		private byte OamAddress = 0x00;
		public bool IsOamDma { get; private set; } = false;
		private DoubleRegister OamDmaAddress = new(0);
		private bool IsOamDmaReading = false;

		public Action NmiRaiser { private get; set; } = null;

		private byte RenderingNametableFetch = 0;
		private byte RenderingAttributeFetch = 0;
		private byte RenderingPatternLowerFetch = 0;
		private byte RenderingPatternUpperFetch = 0;

		private uint Cycle = 0;
		private int Scanline = -1;
		private bool IsOddFrame = false;
		public bool IsFrameReady { get; private set; }

		private readonly byte[,] InterBuffer = new byte[256, 240];
		private static readonly byte[][] BlankBuffer = new byte[][]
		{
			Resources.BlankBitmap1x.CloneArray(),
			Resources.BlankBitmap2x.CloneArray(),
			Resources.BlankBitmap3x.CloneArray(),
			Resources.BlankBitmap4x.CloneArray()
		};

		public int AddressableSize => 8;
		public bool IsReadonly { get; set; }

		private static readonly ImmutableArray<(byte R, byte G, byte B)> MasterPalette = ImmutableArray.Create(new (byte, byte, byte)[]
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
						returnedData = (byte)(Status.Byte | (RegisterAccessNoise & 0x1f));
						Status.VerticalBlank = false;
						RegisterWriteLatch = false; // reading from status resets the write latch for those 2 registers
						break;

					case 4: // OAM Data
						returnedData = Oam[OamAddress];
						break;

					case 7: // data
							// read "The PPUDATA read buffer (post-fetch)" http://wiki.nesdev.com/w/index.php/PPU_registers#PPUDATA
						if (AddressT.Whole < 0x3f00)
						{
							// reading from CHR or nametables
							// this reading is delayed and goes through a buffer
							returnedData = DataBuffer;
							DataBuffer = Memory[AddressT.Whole];
						}
						else
						{
							// reading from palette
							// palette reading has is instant as it's made up of static RAM
							returnedData = Memory[AddressT.Whole];
							// the nametable is still read from the underlying mirrored address
							DataBuffer = Memory[0x2000 | AddressT.Whole & 0x0fff];
						}

						AddressT.Whole = (ushort)((AddressT.Whole + (Control.IncrementMode ? 32 : 1)) & 0x7fff);

						if (Scanline is <= 240) // is rendering
						{
							IncrementAddressVHorizontalCoarse();
							IncrementAddressVVerticalFine();
						}
						else
							CurrentVramAddressV += Control.IncrementMode ? 32 : 1;

						break;

					default:
						break;
				}

				RegisterAccessNoise = returnedData;
				return returnedData;
			}

			set
			{
				RegisterAccessNoise = value;

				switch (address)
				{
					case 0: // control
							// if vblank and GenerateNmi goes from off (low) to on (high)
						if (Status.VerticalBlank && !Control.GenerateNmi && (value & 0x80) != 0)
							NmiRaiser?.Invoke();

						AddressT.Upper = (byte)(AddressT.Upper & 0x73 | ((value & 0x03) << 2));
						Control.Byte = value;
						break;

					case 1: // mask
						Mask.Byte = value;
						break;

					case 3: // OAM address
						OamAddress = value;
						break;

					case 4: // OAM data
						Oam[OamAddress++] = value;
						break;

					case 5: // scroll
							// first x scroll then y scroll (initial value of the latch is 0)
						if (RegisterWriteLatch)
						{
							AddressT.Whole = (ushort)((AddressT.Whole & 0x0c1f)
								| ((value & 0xf8) << 2)                         // coarse Y
								| ((value & 0x03) << 12));                      // fine Y
						}
						else
						{
							AddressT.Lower = (byte)((AddressT.Lower & 0xe0) | (value >> 3)); // set coarse X
							FineScrollX = (byte)(value & 0x3);
						}

						RegisterWriteLatch = !RegisterWriteLatch;
						break;

					case 6: // address
							// first high byte then low byte (initial value of the latch is 0)
						if (RegisterWriteLatch)
						{
							AddressT.Lower = value;
							CurrentVramAddressV = AddressT.Whole;
						}
						else
							AddressT.Upper = (byte)(value & 0x3f);

						RegisterWriteLatch = !RegisterWriteLatch;
						break;

					case 7: // data
						Memory[AddressT.Whole] = value;
						AddressT.Whole += (ushort)((AddressT.Whole + (Control.IncrementMode ? 32 : 1)) & 0x7fff);

						if (Scanline is <= 240) // is rendering
						{
							IncrementAddressVHorizontalCoarse();
							IncrementAddressVVerticalFine();
						}
						else
							CurrentVramAddressV = (ushort)((CurrentVramAddressV + (Control.IncrementMode ? 32 : 1)) & 0x7fff);
						break;

					case 0x14: // OAM DMA
						OamDmaAddress.Upper = value;
						OamDmaAddress.Lower = OamAddress;
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

				if (OamDmaAddress.Lower == OamAddress)
					IsOamDma = false;
			}

			IsOamDmaReading = !IsOamDmaReading;
		}

		public void Tick()
		{
			bool isRenderingEnabled = Mask.ShowBackground || Mask.ShowSprites;

			// scanline specific
			if (Scanline == -1) // -1 or 261 (pre-render scanline)
			{
				// clear flags
				if (Cycle == 1)
				{
					Status.VerticalBlank = false;
					Status.Sprite0Hit = false;
					Status.SpriteOverflow = false;
				}

				// during these cycles, continuosly copy over the vertical bits from T to V
				if (isRenderingEnabled && Cycle is >= 280 and <= 304)
					CurrentVramAddressV = (ushort)((CurrentVramAddressV & 0x041f) | (AddressT.Whole & 0x7be0));

				// TODO: make this skip (0, 0) instead of (340, -1)
				if (IsOddFrame && Cycle == 339) // skip last cycle on odd frames
					Cycle = 340;
			}

			if (Scanline < 240) // -1..239
			{
				if (Cycle > 0)
				{
					switch ((Cycle - 1) & 0x7) // cycles 0 is idle
					{
						case 1: // tile ID fetch
							RenderingNametableFetch = Memory[0x2000 | (CurrentVramAddressV & 0x0fff)];
							break;

						case 3: // tile color attribute
							RenderingAttributeFetch = Memory[0x23c0     // attribute table start
								| (CurrentVramAddressV & 0x0c00)        // nametable ID
								| ((CurrentVramAddressV >> 4) & 0x38)   // high 3 bits of coarse Y
								| ((CurrentVramAddressV >> 2) & 0x07)]; // high 3 bits of coarse X

							// prematurely select the palette, makes things simpler
							RenderingAttributeFetch >>= ((CurrentVramAddressV >> 6) & 0x2) + ((CurrentVramAddressV >> 2) & 1);

							break;

						case 5: // tile pattern lower byte
							RenderingPatternLowerFetch = Memory[0x0000
								| (Control.BackgroundAddress ? 0x1000 : 0) // pattern memory select
								| (RenderingNametableFetch << 4)           // tile ID * 16 (because a tile is 16 bytes in the pattern memory)
								| (CurrentVramAddressV >> 12)];            // fineY
							break;

						case 7: // tile pattern upper byte
								// NOTE: this fetch happens on the last cycle of each scanline and will be skipped
								// on the pre-render scanline on odd frames because that cycle is skipped
							RenderingPatternUpperFetch = Memory[0x0000
								| (Control.BackgroundAddress ? 0x1000 : 0) // pattern memory select
								| (RenderingNametableFetch << 4)           // tile ID * 16 (because a tile is 16 bytes in the pattern memory)
								| (CurrentVramAddressV >> 12)              // fineY
								| (0b1000)];                               // + 8 (for the upper byte)
							break;

						default:
							break;
					}
				}

				if (Cycle is >= 2 and <= 257 or >= 322 and <= 337) // cycles 2..257, 322..337
				{
					BgPatternShifterLower.Whole <<= 1;
					BgPatternShifterUpper.Whole <<= 1;
					BgAttributeShifter.Whole <<= 2; // NOTE: the 2 8-bit attribute shifters have been combined into a single 16-bit shifter
				}

				// increment coarse X
				if ((Cycle & 0x7) == 0 && Cycle is >= 8 and <= 248 or >= 328) // cycles 8, 16, 24 ... 256, 328, 336
					IncrementAddressVHorizontalCoarse();

				// reload shifters
				if ((Cycle & 0x7) == 1 && Cycle is >= 9 and <= 257 or >= 329) // cycles 9, 17, 25 ... 257, 329, 337
				{
					BgPatternShifterLower.Lower = RenderingPatternLowerFetch;
					BgPatternShifterUpper.Lower = RenderingPatternUpperFetch;
					BgAttributeShifter.Lower = RenderingAttributeFetch;
				}

				if (Cycle == 256) // go down at end of scanline
					IncrementAddressVVerticalFine();
				else if (Cycle == 257) // copy horizontal bits from T to V
					CurrentVramAddressV = (ushort)((CurrentVramAddressV & 0x7be0) | (AddressT.Whole & 0x041f));
			}

			if (Scanline is >= 0 and < 240)
			{
				if (Cycle is >= 1 and <= 256)
				{
					int patternBitmask = 0x80 >> FineScrollX;

					int color = ((BgPatternShifterUpper.Upper & patternBitmask) << 1 >> (7 - FineScrollX))
						| ((BgPatternShifterLower.Upper & patternBitmask) >> (7 - FineScrollX));
					int palette = BgAttributeShifter.Upper;

					InterBuffer[Cycle - 1, Scanline] = Memory[0x3f00
						| (palette << 2)
						| (color)];
				}
			}
			else if (Scanline == 241)
			{
				if (Cycle == 1)
				{
					IsFrameReady = true;
					Status.VerticalBlank = true;

					if (Control.GenerateNmi)
						NmiRaiser?.Invoke();
				}
			}

			// cycle and scanline counting
			Cycle++;

			if (Cycle > 340)
			{
				Cycle = 0;
				Scanline++;

				if (Scanline > 260)
				{
					Scanline = -1;
				}
			}

			if (AddressT.Whole > 0x7fff || CurrentVramAddressV > 0x7fff)
				throw new Exception("!!!!");
		}

		private void IncrementAddressVHorizontalCoarse()
		{
			if ((CurrentVramAddressV & 0x001f) == 31) // if coarse X == 31
			{
				CurrentVramAddressV &= ~0x001f;       // reset coarse X to 0
				CurrentVramAddressV ^= 0x0400;        // switch horizontal nametable
			}
			else
				CurrentVramAddressV++;                // increment coarse X
		}

		private void IncrementAddressVVerticalFine()
		{
			if ((CurrentVramAddressV & 0x7000) != 0x7000)          // fine Y < 7
				CurrentVramAddressV += 0x1000;                     // increment fine Y
			else
			{
				CurrentVramAddressV &= ~0x7000;                    // set fine Y to 0
				int coarseY = (CurrentVramAddressV & 0x03e0) >> 5; // coarse Y

				if (coarseY == 29)
				{
					coarseY = 0;                                   // set coarse Y to 0
					CurrentVramAddressV ^= 0x0800;                 // switch vertical nametable
				}
				else if (coarseY == 31)
					coarseY = 0;                                   // set coarse Y to 0, don't switch nametables
				else
					coarseY++;                                     // increment coarse Y

				CurrentVramAddressV = (ushort)((CurrentVramAddressV & ~0x03e0) | (coarseY << 5));
			}
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
					var color = MasterPalette[interFrame[x / scale, y / scale] & 0x3f];
					buffer[start + (fixedY * 256 * scale + x) * 3 + 0] = color.B; // B
					buffer[start + (fixedY * 256 * scale + x) * 3 + 1] = color.G; // G
					buffer[start + (fixedY * 256 * scale + x) * 3 + 2] = color.R; // R
				}
			}

			return buffer;
		}

		public byte[,] GetFrame()
		{
			IsFrameReady = false;
			return InterBuffer;

			/*
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
			*/
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

		public byte[] DumpPaletteMemory()
		{
			byte[] paletteMemory = new byte[32];

			for (int i = 0x3f00; i <= 0x3f1f; i++)
			{
				paletteMemory[i & 0x1f] = Memory[i];
			}

			return paletteMemory;
		}
	}
}
