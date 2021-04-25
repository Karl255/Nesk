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
		private DoubleRegister BgAttributeShifterLower = new(0);
		private DoubleRegister BgAttributeShifterUpper = new(0);

		private readonly byte[] SpritePatternShiftersUpper = new byte[8];
		private readonly byte[] SpritePatternShiftersLower = new byte[8];
		private readonly byte[] SpriteAttributeLatches = new byte[8];
		private readonly byte[] SpriteXPositionCounters = new byte[8];
		private int SpriteIndex = 0;
		private int SecondaryOamIndex = 0;
		private bool LineHasSprite0 = false;
		private bool IsSprite0Rendered;
		private bool IsSpriteEvaluationDone = false;
		private int RenderingSpriteIndex = 0;

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
						returnedData = (byte)Status.Byte;
						Status.VerticalBlank = false;
						RegisterWriteLatch = false; // reading from status resets the write latch for those 2 registers
						break;

					case 4: // OAM Data
						if (Cycle <= 64)
							returnedData = 0xff;
						else
							returnedData = Oam[OamAddress];
						break;

					case 7: // data
							// read "The PPUDATA read buffer (post-fetch)" http://wiki.nesdev.com/w/index.php/PPU_registers#PPUDATA
						if ((AddressT.Whole & 0x3fff) < 0x3f00)
						{
							// reading from CHR or nametables
							// this reading is delayed and goes through a buffer
							returnedData = DataBuffer;
							DataBuffer = Memory[AddressT.Whole & 0x3fff];
						}
						else
						{
							// reading from palette
							// palette reading has is instant as it's made up of static RAM
							returnedData = Memory[AddressT.Whole & 0x3fff];
							// the nametable is still read from the underlying mirrored address
							DataBuffer = Memory[0x2000 | AddressT.Whole & 0x0fff];
						}

						AddressT.Whole += (ushort)(Control.IncrementMode ? 32 : 1);

						if (Scanline is >= 0 and < 240 && (Mask.ShowBackground || Mask.ShowSprites)) // is rendering
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
							FineScrollX = (byte)(value & 0x7);
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
						Memory[AddressT.Whole & 0x3fff] = value;
						AddressT.Whole += (ushort)(Control.IncrementMode ? 32 : 1);

						if (Scanline is >= 0 and < 240 && (Mask.ShowBackground || Mask.ShowSprites)) // is rendering
						{
							IncrementAddressVHorizontalCoarse();
							IncrementAddressVVerticalFine();
						}
						else
							CurrentVramAddressV += (ushort)(Control.IncrementMode ? 32 : 1);
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
			bool isRenderingEnabled = Mask.ShowBackground || Mask.ShowSprites || true;

			if (Scanline == 41 && Cycle == 21)
			{

			}

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
				if (Cycle is >= 280 and <= 304)
					CurrentVramAddressV = (ushort)((CurrentVramAddressV & 0x041f) | (AddressT.Whole & 0x7be0));
			}

			if (Scanline < 240) // -1..239
			{
				if (Cycle is > 0 and <= 256 or >= 321)
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
							RenderingAttributeFetch >>= ((CurrentVramAddressV >> 4) & 0x4) + ((CurrentVramAddressV) & 2);

							break;

						case 5: // tile pattern lower byte
							RenderingPatternLowerFetch = Memory[0x0000
								| (Control.BackgroundAddress ? 0x1000 : 0) // pattern memory select
								| (RenderingNametableFetch << 4)           // tile ID * 16 (because a tile is 16 bytes in the pattern memory)
								| ((CurrentVramAddressV >> 12) & 0x7)];    // fineY
							break;

						case 7: // tile pattern upper byte
								// NOTE: this fetch happens on the last cycle of each scanline and will be skipped
								// on the pre-render scanline on odd frames because that cycle is skipped
							RenderingPatternUpperFetch = Memory[0x0000
								| (Control.BackgroundAddress ? 0x1000 : 0) // pattern memory select
								| (RenderingNametableFetch << 4)           // tile ID * 16 (because a tile is 16 bytes in the pattern memory)
								| ((CurrentVramAddressV >> 12) & 0x7)      // fineY
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
					BgAttributeShifterLower.Whole <<= 1;
					BgAttributeShifterUpper.Whole <<= 1;
				}

				// reload shifters
				if ((Cycle & 0x7) == 1 && Cycle is >= 9 and <= 257 or >= 329) // cycles 9, 17, 25 ... 257, 329, 337
				{
					BgPatternShifterLower.Lower = RenderingPatternLowerFetch;
					BgPatternShifterUpper.Lower = RenderingPatternUpperFetch;
					BgAttributeShifterLower.Lower = (byte)((RenderingAttributeFetch & 0b01) != 0 ? 0xff : 0);
					BgAttributeShifterUpper.Lower = (byte)((RenderingAttributeFetch & 0b10) != 0 ? 0xff : 0);
				}

				// increment coarse X
				if ((Cycle & 0x7) == 0 && Cycle is >= 8 and <= 248 or >= 328) // cycles 8, 16, 24 ... 248, 328, 336
					IncrementAddressVHorizontalCoarse();

				if (Cycle == 256) // go down at end of scanline
					IncrementAddressVVerticalFine();
				else if (Cycle == 257) // copy horizontal bits from T to V
					CurrentVramAddressV = (ushort)((CurrentVramAddressV & 0x7be0) | (AddressT.Whole & 0x041f));

			}

			if (Scanline is >= 0 and < 240) // visible scanlines
			{
				if (Cycle == 0) // variable reset
				{
					SpriteIndex = 0;
					SecondaryOamIndex = 0;
					IsSpriteEvaluationDone = false;
					LineHasSprite0 = false;

					for (int i = 0; i < 8; i++)
						SpriteXPositionCounters[i] = 0xff;
				}

				// secondary OAM initialization
				else if (Cycle <= 64) // cycles 1..64
					SecondaryOam[(Cycle - 1) & 0x1f] = 0xff;

				// sprite evaluation
				else if (Cycle <= 256) // cycles 65..256
				{
					if (SecondaryOamIndex < 8 && !IsSpriteEvaluationDone)
					{
						if ((Cycle & 1) == 1) // odd cycles
						{
							// everything is happenig in the odd cycle because of simplyicity
							int y = SecondaryOam[4 * SecondaryOamIndex] = Oam[4 * SpriteIndex];

							if (y <= Scanline && y > Scanline - (Control.SpriteHeight ? 16 : 8)) // is the sprite within range
							{
								SecondaryOam[4 * SecondaryOamIndex + 1] = Oam[4 * SpriteIndex + 1];
								SecondaryOam[4 * SecondaryOamIndex + 2] = Oam[4 * SpriteIndex + 2];
								SecondaryOam[4 * SecondaryOamIndex + 3] = Oam[4 * SpriteIndex + 3];
								SecondaryOamIndex++;

								if (SpriteIndex == 0)
									LineHasSprite0 = true;
							}

							SpriteIndex++;

							if (SpriteIndex == 64 || SecondaryOamIndex == 8) // if it overflowed
							{
								IsSpriteEvaluationDone = true;

								// checking for sprite overflow
								// the PPU has a bug in the sprite overflow detection where m is incremented (though it shoudln't)
								// so the incorrect data is fetched for the Y position
								if (SecondaryOamIndex == 0)
								{
									int m = 0;
									for (int n = 0; n < 64; n++)
									{
										int y2 = Oam[4 * n + m];

										if (y2 <= Scanline && y2 > Scanline - (Control.SpriteHeight ? 16 : 8))
										{
											Status.SpriteOverflow = true;
											break;
										}
										m = (m + 1) & 0x3;
									}
								}
							}
						}
					}
				}
				else if (Cycle <= 320)
				{
					int currentSpriteIndex = (int)((Cycle - 257) >> 3);

					switch ((Cycle - 1) & 0x7)
					{
						case 5:
							SpritePatternShiftersLower[currentSpriteIndex] = FetchSpritePattern(currentSpriteIndex, 0);
							break;

						case 7:
							SpritePatternShiftersUpper[currentSpriteIndex] = FetchSpritePattern(currentSpriteIndex, 8);
							break;

						default:
							break;
					}
				}

				// final pixel processing

				if (Cycle == 0)
					RenderingSpriteIndex = 0;
				else if (Cycle <= 256) // cycles 1..256
				{
					int spriteColor = 0;
					int spritePalette = 0;
					int spritePriority = 0;

					for (int i = 0; i < SecondaryOamIndex; i++) // only go through the indecies which have been filled
					{
						int spriteX = SecondaryOam[4 * i + 3];
						if (spriteX >= Cycle && spriteX < Cycle + 8)
						{
							int shiftAmount = 7 - ((int)Cycle - spriteX);
							int tempColor = ((SpritePatternShiftersUpper[i] << 1 >> shiftAmount) & 0b10)
								| ((SpritePatternShiftersLower[i] >> shiftAmount) & 0b01);

							if (spriteColor == 0)
							{
								spriteColor = tempColor;
								spritePalette = SecondaryOam[4 * i + 2] & 0b11;
								spritePriority = (SecondaryOam[4 * i + 2] >> 5) & 1;
							}
							else
								break;
						}

						if (LineHasSprite0 && i == 0) // detect if this is sprite 0
							IsSprite0Rendered = true;
					}


					int backgroundPatternBitmask = 0x80 >> FineScrollX;

					int backgroundColor = ((BgPatternShifterUpper.Upper & backgroundPatternBitmask) << 1 >> (7 - FineScrollX))
						| ((BgPatternShifterLower.Upper & backgroundPatternBitmask) >> (7 - FineScrollX));
					int backgroundPalette = ((BgAttributeShifterUpper.Upper & backgroundPatternBitmask) << 1 >> (7- FineScrollX))
						| ((BgAttributeShifterLower.Upper & backgroundPatternBitmask) >> (7 - FineScrollX));


					InterBuffer[Cycle - 1, Scanline] = backgroundColor == 0
						? Memory[0x3f00]
						: Memory[0x3f00 | (backgroundPalette << 2) | (backgroundColor)];

					// I've put <= 0 instead od simply 0, because, with <= 0, the compiler can better optimize the pattern matching
					// an alternative would be using uint instead of int
					InterBuffer[Cycle - 1, Scanline] = Memory[0x3f00
						| (backgroundColor, spriteColor, spritePriority) switch
						{
							(<= 0, <= 0, _) => 0,                                                    // background color
							(<= 0,  > 0, _) => 0b10000 | (spritePalette     << 2) | spriteColor,     // sprite color
							( > 0, <= 0, _) =>           (backgroundPalette << 2) | backgroundColor, // background tile color
							( > 0,  > 0, 0) => 0b10000 | (spritePalette     << 2) | spriteColor,     // sprite color
							_               =>           (backgroundPalette << 2) | backgroundColor, // background tile color
						}];
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

			// skip first cycle on odd frames
			if (IsOddFrame && Cycle == 0 && Scanline == 0)
				Cycle = 1;
		}

		private void IncrementAddressVHorizontalCoarse()
		{
			if ((CurrentVramAddressV & 0x001f) == 31) // if coarse X == 31
			{
				CurrentVramAddressV &= 0x7fe0;        // reset coarse X to 0
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
				CurrentVramAddressV &= 0x0fff;                     // set fine Y to 0
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

				CurrentVramAddressV = (ushort)((CurrentVramAddressV & 0x7c1f) | (coarseY << 5));
			}
		}

		private byte FetchSpritePattern(int spriteIndex, int offset)
		{
			bool isVerticallyMirrored = (SecondaryOam[4 * spriteIndex + 2] & 0x80) != 0;
			byte spriteY = SecondaryOam[4 * spriteIndex];
			byte spritePatternID = SecondaryOam[4 * spriteIndex + 1];

			int address;

			if (Control.SpriteHeight) // 8x16 sprites
			{
				if (isVerticallyMirrored) // vertical mirroring
				{
					if (Scanline - spriteY < 8) // top tile
					{
						address = ((spritePatternID & 1) << 12)
						   | (((spritePatternID & 0xfe) | 1) << 4)
						   | (7 - (Scanline - spriteY) + offset);
					}
					else // bottom tile
					{
						address = ((spritePatternID & 1) << 12)
							| ((spritePatternID & 0xfe) << 4)
							| (7 - (Scanline - spriteY - 8) + offset);
					}
				}
				else // no vertical mirroring
				{
					if (Scanline - spriteY < 8) // top tile
					{
						address = ((spritePatternID & 1) << 12)
							| ((spritePatternID & 0xfe) << 4)
							| (Scanline - spriteY + offset);
					}
					else
					{
						address = ((spritePatternID & 1) << 12)
							| ((spritePatternID & 0xfe) << 4)
							| (Scanline - spriteY - 8 + offset);
					}
				}
			}
			else // 8x8 sprites
			{
				if (isVerticallyMirrored) // vertical mirroring
				{
					address = (spritePatternID << 12)
						| (spritePatternID << 4)
						| (7 - (Scanline - spriteY + offset));
				}
				else // no vertical mirroring
				{
					address = (spritePatternID << 12)
						| (spritePatternID << 4)
						| (Scanline - spriteY + offset);
				}
			}

			return Memory[address];
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
