using System;
using K6502Emu;
using Nesk.Mappers;

namespace Nesk
{
	public sealed class Nesk
	{
		private K6502 Cpu { get; init; }
		private CpuMapper CpuBus { get; init; }
		private Ppu Ppu { get; init; }
		private PpuMapper PpuBus { get; init; }
		private IAddressable<byte> Apu { get; init; }
		private long TickCount = 0;

		public double FrameRate;

		public Nesk(Cartridge cartridge, Func<uint> readInputCallback)
		{
			PpuBus = cartridge.GetPPUMapper();
			Ppu = new Ppu(PpuBus);
			Apu = null; // TODO: implement this when APU is implemented

			CpuBus = cartridge.GetCPUMapper(Ppu, Apu, readInputCallback);
			Cpu = new K6502(CpuBus, false);
			Ppu.NmiRaiser = Cpu.SetNmi;

			FrameRate = cartridge.TimingMode == TimingMode.NTSC ? 29.97 : 25.00;
		}

		private void Tick()
		{
			TickCount++;
			TickCount %= 3;

			// the PPU clock is 3x faster than the CPU clock
			if (TickCount % 3 == 0)
			{
				if (Ppu.IsOamDma)
					Ppu.DoOamDma(CpuBus);
				else
					Cpu.Tick();
			}

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

#if DEBUG
		public byte[] RenderPatternMemory(int palette) => Ppu.RenderPatternMemory(palette);

		/// <summary>
		/// Dumps the whole memory space to a byte array. PPU, APU and IO register locations are filled with <c>0xff</c>.
		/// </summary>
		/// <returns></returns>
		public byte[] DumpMemory()
		{
			byte[] dump = new byte[64 * 1024];

			var memory = Cpu.GetMemory;

			for (int i = 0; i < 0x2000; i++)
				dump[i] = memory[i];

			for (int i = 0x2000; i < 0x4020; i++)
				dump[i] = 0x00;

			for (int i = 0x4020; i < 0xffff; i++)
				dump[i] = memory[i];

			return dump;
		}
#endif
	}
}
