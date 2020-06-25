using K6502Emu;
using System.Threading.Channels;

namespace Nesk
{
	public sealed class Nesk
	{
		public bool IsNewFrameReady => _isNewFrameReady;
		private bool _isNewFrameReady = false;

		private K6502 Cpu;

		public Nesk()
		{
			var bus = new Bus()
			{
				//TODO: add components
			};

			Cpu = new K6502(bus);
		}

		public byte[] GetFrame()
		{
			//TODO: change this when PPU is implemented
			if (_isNewFrameReady)
				return new byte[] { };
			else
				return null;
		}

		public void Tick()
		{
			//TODO: implement ticking
			//Cpu.Tick();
		}
	}
}
