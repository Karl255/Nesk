namespace Nesk.Shared
{
	public static class Extensions
	{
		public static int GetBit(this byte @this, int index) =>
			(@this >> index) & 1;

		public static int ToInt(this bool @this) => @this ? 1 : 0;

		public static T[] CloneArray<T>(this T[] @this) => @this.Clone() as T[];
	}
}
