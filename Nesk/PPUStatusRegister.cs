using Nesk.Shared;

namespace Nesk
{
	public struct PPUStatusRegister
	{
		public bool SpriteOverflow;
		public bool Sprite0Hit;
		public bool VerticalBlank;

		public PPUStatusRegister(byte value)
		{
			SpriteOverflow = (value & 0b0010_0000) != 0;
			Sprite0Hit     = (value & 0b0100_0000) != 0;
			VerticalBlank  = (value & 0b1000_0000) != 0;
		}

		public int Byte
		{
			get
			{
				int data = (SpriteOverflow.ToInt() << 5)
					|      (Sprite0Hit.ToInt()     << 6)
					|      (VerticalBlank.ToInt()  << 7);

				VerticalBlank = false;

				return data;
			}

			set
			{
				SpriteOverflow = (value & 0b0010_0000) != 0;
				Sprite0Hit     = (value & 0b0100_0000) != 0;
				VerticalBlank  = (value & 0b1000_0000) != 0;
			}
		}
	}
}
