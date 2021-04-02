using K6502Emu;
using Nesk.Mappers;

namespace Nesk
{
	public sealed class Nesk
	{
		public double FrameRate;

		private K6502 Cpu { get; init; }
		private PPU Ppu { get; init; }
		private long TickCount = 0;

		public Nesk(Cartridge cartridge)
		{
			var ppuBus = cartridge.GetPPUMapper();
			var ppu = new PPU(ppuBus);
			IAddressable<byte> apu = null; // TODO: implement this when APU is implemented

			var cpuBus = cartridge.GetCPUMapper(ppu, apu);
			var cpu = new K6502(cpuBus, false);

			FrameRate = cartridge.TimingMode == TimingMode.NTSC ? 29.97 : 25.00;
			Cpu = cpu;
			Ppu = ppu;
		}

		private void Tick()
		{
			TickCount++;
			TickCount %= 3;

			// the PPU clock is 3x faster than the CPU clock
			if (TickCount % 3 == 0)
				Cpu.Tick();

			Ppu.Tick();
		}

		public byte[] TickToNextFrame()
		{
			while (!Ppu.IsFrameReady)
			{
				Tick();
			}

			return Ppu.GetFrame();
		}

		public byte[] RenderPatternMemory() => Ppu.RenderPatternMemory();
	}
}
