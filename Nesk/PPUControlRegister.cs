using Nesk.Shared;

namespace Nesk
{
	public struct PPUControlRegister
	{
		public int  NametableAddress  { get; set; } // 2 bit
		public bool IncrementMode     { get; set; }
		public bool SpriteAddress     { get; set; }
		public bool BackgroundAddress { get; set; }
		public bool SpriteSize        { get; set; }
		public bool MasterSlave       { get; set; }
		public bool GenerateNMI       { get; set; }

		public PPUControlRegister(byte value)
		{
			NametableAddress  =  value & 0b0000_0011;
			IncrementMode     = (value & 0b0000_0100) != 0;
			SpriteAddress     = (value & 0b0000_1000) != 0;
			BackgroundAddress = (value & 0b0001_0000) != 0;
			SpriteSize        = (value & 0b0010_0000) != 0;
			MasterSlave       = (value & 0b0100_0000) != 0;
			GenerateNMI       = (value & 0b1000_0000) != 0;
		}

		public int Byte
		{
			get => NametableAddress
				| (IncrementMode.ToInt()     << 2)
				| (SpriteAddress.ToInt()     << 3)
				| (BackgroundAddress.ToInt() << 4)
				| (SpriteSize.ToInt()        << 5)
				| (MasterSlave.ToInt()       << 6)
				| (GenerateNMI.ToInt()       << 7);

			set
			{
				NametableAddress  =  value & 0b0000_0011;
				IncrementMode     = (value & 0b0000_0100) != 0;
				SpriteAddress     = (value & 0b0000_1000) != 0;
				BackgroundAddress = (value & 0b0001_0000) != 0;
				SpriteSize        = (value & 0b0010_0000) != 0;
				MasterSlave       = (value & 0b0100_0000) != 0;
				GenerateNMI       = (value & 0b1000_0000) != 0;
			}
		}
	}
}
