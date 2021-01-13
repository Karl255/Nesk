using K6502Emu;
using System;

namespace Nesk
{
	public sealed class Nesk
	{
		// TODO: properly implement this
		public double FrameRate = 29.97;

		private K6502 Cpu { get; init; }
		private readonly byte[] BlankBuffer = Shared.Resources.BlankBitmap;
		private long Cycle = 0;
		private long Scanline = 0;
		private bool IsFrameComplete = false;

		public Nesk()
		{
			var bus = new Bus(64 * 1024) // 64 kB
			{
				//TODO: add components
			};

			Cpu = new K6502(bus, false);
		}

		private void Tick()
		{
			Cpu.Tick();
			//Ppu.Tick();

			Cycle++;

			if (Cycle >= 341)
			{
				Cycle = 0;
				Scanline++;

				if (Scanline >= 261)
				{
					Scanline = -1;
					IsFrameComplete = true;
				}
			}
		}

		public byte[] TickToNextFrame()
		{
			while (!IsFrameComplete)
			{
				Tick();
			}

			IsFrameComplete = false;

			// generate greyscale noise and return that as the frame
			byte[] frameBuffer = (byte[])BlankBuffer.Clone();
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
	}
}
