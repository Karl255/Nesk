using Nesk.Shared;

namespace Nesk
{
	public struct PpuControlRegister
	{
		/// <summary>
		/// <list type="bullet">
		///		<item>
		///			0: 0x2000
		///		</item>
		///		<item>
		///			1: 0x2400
		///		</item>
		///		<item>
		///			2: 0x2800
		///		</item>
		///		<item>
		///			3: 0x2c00
		///		</item>
		/// </list>
		/// </summary>
		public int  NametableAddress  { get; set; } // 2 bit

		/// <summary>
		/// Automatically increment address after read/write.
		/// <list type="bullet">
		///		<item>
		///			0: +1
		///		</item>
		///		<item>
		///			1: +32
		///		</item>
		/// </list>
		/// </summary>
		public bool IncrementMode     { get; set; }

		/// <summary>
		/// Chooses the pattern table section to be used for sprites.
		/// </summary>
		public bool SpriteSection     { get; set; }

		/// <summary>
		/// Chooses the pattern table section to be used for background tiles.
		/// </summary>
		public bool BackgroundAddress { get; set; }

		/// <summary>
		/// Chooses the sprite size:
		/// <list type="bullet">
		///		<item>
		///			0: 8x8
		///		</item>
		///		<item>
		///			1: 8x16
		///		</item>
		/// </list>
		/// </summary>
		public bool SpriteSize        { get; set; }

		/// <summary>
		/// Does essentially nothing.
		/// </summary>
		public bool MasterSlave       { get; set; }
		public bool GenerateNmi       { get; set; }

		public PpuControlRegister(byte value)
		{
			NametableAddress  =  value & 0b0000_0011;
			IncrementMode     = (value & 0b0000_0100) != 0;
			SpriteSection     = (value & 0b0000_1000) != 0;
			BackgroundAddress = (value & 0b0001_0000) != 0;
			SpriteSize        = (value & 0b0010_0000) != 0;
			MasterSlave       = (value & 0b0100_0000) != 0;
			GenerateNmi       = (value & 0b1000_0000) != 0;
		}

		public int Byte
		{
			get => NametableAddress
				| (IncrementMode.ToInt()     << 2)
				| (SpriteSection.ToInt()     << 3)
				| (BackgroundAddress.ToInt() << 4)
				| (SpriteSize.ToInt()        << 5)
				| (MasterSlave.ToInt()       << 6)
				| (GenerateNmi.ToInt()       << 7);

			set
			{
				NametableAddress  =  value & 0b0000_0011;
				IncrementMode     = (value & 0b0000_0100) != 0;
				SpriteSection     = (value & 0b0000_1000) != 0;
				BackgroundAddress = (value & 0b0001_0000) != 0;
				SpriteSize        = (value & 0b0010_0000) != 0;
				MasterSlave       = (value & 0b0100_0000) != 0;
				GenerateNmi       = (value & 0b1000_0000) != 0;
			}
		}
	}
}
