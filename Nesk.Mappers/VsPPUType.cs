namespace Nesk.Mappers
{
	public enum VsPPUType
	{
		// these have different color palettes, refer to: http://wiki.nesdev.com/w/index.php/PPU_palettes
		RP2C03B,
		RP2C03G,
		RP2C04_0001,
		RP2C04_0002,
		RP2C04_0003,
		RP2C04_0004,
		RC2C03B,
		RC2C03C,
		// the following PPUs swap registers 0x2000 and 0x2001 and return a signature in the lower bits of 0x2002
		// source: http://wiki.nesdev.com/w/index.php/NES_2.0#Vs._System_Type
		RC2C05_01,
		RC2C05_02,
		RC2C05_03,
		RC2C05_04,
		RC2C05_05
	}
}
