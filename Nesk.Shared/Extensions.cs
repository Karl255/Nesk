namespace Nesk.Shared
{
	public static class Extensions
	{
		public static int GetBit(this byte @this, int index) =>
			(@this >> index) & 1;
	}
}
