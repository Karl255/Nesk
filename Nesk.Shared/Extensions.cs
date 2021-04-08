namespace Nesk.Shared
{
	public static class Extensions
	{
		public static int GetBit(this byte @this, int index) =>
			(@this >> index) & 1;

		public static int ToInt(this bool @this) => @this ? 1 : 0;

		public static T[] CloneArray<T>(this T[] @this) => @this.Clone() as T[];

		public static void FillArray<T>(this T[,] @this, int size1, int size2, T value)
		{
			for (int j = 0; j < size2; j++)
				for (int i = 0; i < size1; i++)
					@this[i, j] = value;
		}
	}
}
