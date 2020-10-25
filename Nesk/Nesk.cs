using K6502Emu;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Nesk
{
	public sealed class Nesk
	{
		/// <summary>
		/// The ChannelReader used to retrieve the frame buffer (byte array containing a bitmap image) for each frame.
		/// </summary>
		public ChannelReader<byte[]> VideoOutputChannelReader => VideoOutputChannel.Reader;

		private Channel<byte[]> VideoOutputChannel;
		private K6502 Cpu;
		private readonly byte[] BlankBuffer;
		private long TickCounter = 0;

		public Nesk(string romPath)
		{
			BlankBuffer = Shared.Resources.BlankBitmap;

			var bus = new Bus()
			{
				//TODO: add components
			};

			Cpu = new K6502(bus);

			VideoOutputChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(32)
			{
				SingleWriter = true,
				SingleReader = true
			});

		}

		/// <summary>
		/// Calling this method starts an automatic calling of TickAsync every ~0.5 us forever, until canceled using the specified token.
		/// </summary>
		/// <param name="token">The token with which the ticking is stopped.</param>
		public async void RunAsync(CancellationToken token)
		{
			while (!token.IsCancellationRequested)
			{
				long t1 = Stopwatch.GetTimestamp();
				await TickAsync();

				/*
				 * 0.5 us delay, although it should be 0.558659217877095 us (microseconds)
				 * the method returns the amount of ticks counted at a frequency of 10 MHz
				 * (at least on my machine, TODO: make this universal), so every tick is
				 * 100 ns or 0.1 us
				 */
				while (Stopwatch.GetTimestamp() - t1 < 5)
					Thread.Sleep(0);
			}
		}

		/// <summary>
		/// Executes one tick of the machine asynchronously.
		/// </summary>
		/// <returns>A <see cref="System.Threading.Tasks.Task"/> that represents the asynchronous ticking operation.</returns>
		public async Task TickAsync()
		{
			TickCounter++;
			if (TickCounter < 16_639) //create new frames at ~60 Hz
				return;

			TickCounter = 0;
			//TODO: implement proper ticking
			//Cpu.Tick();

			//create random noise and write it to the VideoOutputChannel
			byte[] buffer = (byte[])BlankBuffer.Clone();
			int start = buffer[0x0A];
			var rand = new Random();

			for (int y = 0; y < 240; y++)
			{
				for (int x = 0; x < 256; x++)
				{
					byte v = (byte)rand.Next();
					buffer[start + (y * 256 + x) * 3 + 0] = v; //B
					buffer[start + (y * 256 + x) * 3 + 1] = v; //G
					buffer[start + (y * 256 + x) * 3 + 2] = v; //R
				}
			}

			await VideoOutputChannel.Writer.WriteAsync(buffer);
		}
	}
}
