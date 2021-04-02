using Nesk.Shared;

namespace Nesk
{
	public struct PpuMaskRegister
	{
		public bool Greyscale;
		public bool ShowLeftmostBackground;
		public bool ShowLeftmostSprites;
		public bool ShowBackground;
		public bool ShowSprites;
		public bool EmpasizeRed;
		public bool EmpasizeGreen;
		public bool EmpasizeBlue;

		public PpuMaskRegister(byte value)
		{
			Greyscale              = (value & 0b0000_0001) != 0;
			ShowLeftmostBackground = (value & 0b0000_0010) != 0;
			ShowLeftmostSprites    = (value & 0b0000_0100) != 0;
			ShowBackground         = (value & 0b0000_1000) != 0;
			ShowSprites            = (value & 0b0001_0000) != 0;
			EmpasizeRed            = (value & 0b0010_0000) != 0;
			EmpasizeGreen          = (value & 0b0100_0000) != 0;
			EmpasizeBlue           = (value & 0b1000_0000) != 0;
		}

		public int Byte
		{
			get => (Greyscale.ToInt()              << 0)
				|  (ShowLeftmostBackground.ToInt() << 1)
				|  (ShowLeftmostSprites.ToInt()    << 2)
				|  (ShowBackground.ToInt()         << 3)
				|  (ShowSprites.ToInt()            << 4)
				|  (EmpasizeRed.ToInt()            << 5)
				|  (EmpasizeGreen.ToInt()          << 6)
				|  (EmpasizeBlue.ToInt()           << 7);

			set
			{
				Greyscale              = (value & 0b0000_0001) != 0;
				ShowLeftmostBackground = (value & 0b0000_0010) != 0;
				ShowLeftmostSprites    = (value & 0b0000_0100) != 0;
				ShowBackground         = (value & 0b0000_1000) != 0;
				ShowSprites            = (value & 0b0001_0000) != 0;
				EmpasizeRed            = (value & 0b0010_0000) != 0;
				EmpasizeGreen          = (value & 0b0100_0000) != 0;
				EmpasizeBlue           = (value & 0b1000_0000) != 0;
			}
		}
	}
}
