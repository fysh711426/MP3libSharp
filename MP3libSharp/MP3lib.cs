using System;
using System.IO;
using System.Linq;
using System.Text;

namespace MP3libSharp
{
	// MPEG version enum.
	public enum MPEGVersion : byte
	{
		MPEGVersion2_5 = 0,
		MPEGVersionReserved,
		MPEGVersion2,
		MPEGVersion1,
	}

	// MPEG layer enum.
	public enum MPEGLayer : byte
	{
		MPEGLayerReserved = 0,
		MPEGLayerIII,
		MPEGLayerII,
		MPEGLayerI,
	}

	// Channel mode enum.
	public enum ChannelMode : byte
	{
		Stereo = 0,
		JointStereo,
		DualChannel,
		Mono,
	}

	// MP3Frame represents an individual frame parsed from an MP3 stream.
	public class MP3Frame
	{
		public MPEGVersion MPEGVersion;
		public MPEGLayer MPEGLayer;
		public bool CrcProtection;
		public int BitRate;
		public int SamplingRate;
		public bool PaddingBit;
		public bool PrivateBit;
		public ChannelMode ChannelMode;
		public byte ModeExtension;
		public bool CopyrightBit;
		public bool OriginalBit;
		public byte Emphasis;
		public int SampleCount;
		public int FrameLength;
		public byte[] RawBytes;
	}

	// ID3v1Tag represents an ID3v1 metadata tag.
	public class ID3v1Tag
	{
		public byte[] RawBytes;
	}

	// ID3v2Tag represents an ID3v2 metadata tag.
	public class ID3v2Tag
	{
		public byte[] RawBytes;
	}

	public class MP3lib
	{
		// Library version.
		public const string Version = "1.0.0";

		// Flag controlling the display of debugging information.
		public static bool DebugMode = false;

		// Bit rates.
		public static readonly int[] v1l1_br = new int[] { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448 };
		public static readonly int[] v1l2_br = new int[] { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384 };
		public static readonly int[] v1l3_br = new int[] { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 };
		public static readonly int[] v2l1_br = new int[] { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256 };
		public static readonly int[] v2l2_br = new int[] { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 };
		public static readonly int[] v2l3_br = new int[] { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 };

		// Sampling rates.
		public static readonly int[] v1_sr = new int[] { 44100, 48000, 32000 };
		public static readonly int[] v2_sr = new int[] { 22050, 24000, 16000 };
		public static readonly int[] v25_sr = new int[] { 11025, 12000, 8000 };

		// NextFrame loads the next MP3 frame from the input stream. Skips over ID3
		// tags and unrecognised/garbage data in the stream. Returns nil when the
		// stream has been exhausted.
		public static MP3Frame NextFrame(Stream stream)
		{
			while (true)
			{
				var obj = NextObject(stream);
				if (obj == null)
					return null;
				else if (obj is MP3Frame)
					return obj as MP3Frame;
				else if (obj is ID3v1Tag)
					debug("NextFrame: skipping ID3v1 tag");
				else if (obj is ID3v2Tag)
					debug("NextFrame: skipping ID3v2 tag");
			}
		}

		// NextID3v2Tag loads the next ID3v2 tag from the input stream, skipping all
		// other data. Returns nil when the stream has been exhausted.
		public static ID3v2Tag NextID3v2Tag(Stream stream)
		{
			while (true)
			{
				var obj = NextObject(stream);
				if (obj == null)
					return null;
				else if (obj is MP3Frame)
					debug("NextID3v2Tag: skipping MP3 frame");
				else if (obj is ID3v1Tag)
					debug("NextID3v2Tag: skipping ID3v1 tag");
				else if (obj is ID3v2Tag)
					return obj as ID3v2Tag;
			}
		}

		// NextObject loads the next recognised object from the input stream. Skips
		// over unrecognised/garbage data. Returns *MP3Frame, *ID3v1Tag, *ID3v2Tag,
		// or nil when the stream has been exhausted.
		public static object NextObject(Stream stream)
		{

			// Each MP3 frame begins with a 4-byte header.
			var buffer = new byte[4];

			// Fill the header buffer.
			var ok = fillBuffer(stream, buffer, 0, buffer.Length);
			if (!ok)
				return null;

			// Scan forward until we find an object or reach the end of the stream.
			while (true)
			{
				// Check for an ID3v1 tag: 'TAG'.
				if (buffer[0] == 84 && buffer[1] == 65 && buffer[2] == 71)
				{
					var tag = new ID3v1Tag();
					tag.RawBytes = new byte[128];
					Buffer.BlockCopy(buffer, 0, tag.RawBytes, 0, buffer.Length);

					ok = fillBuffer(stream, tag.RawBytes, 4, tag.RawBytes.Length - 4);
					if (!ok)
						return null;
					return tag;
				}

				// Check for an ID3v2 tag: 'ID3'.
				if (buffer[0] == 73 && buffer[1] == 68 && buffer[2] == 51)
				{
					// Read the remainder of the 10 byte tag header.
					var remainder = new byte[6];
					ok = fillBuffer(stream, remainder, 0, remainder.Length);
					if (!ok)
						return null;

					// The last 4 bytes of the header indicate the length of the tag.
					// This length does not include the header itself.
					var length =
						(((int)remainder[2]) << (7 * 3)) |
						(((int)remainder[3]) << (7 * 2)) |
						(((int)remainder[4]) << (7 * 1)) |
						(((int)remainder[5]) << (7 * 0));

					var tag = new ID3v2Tag();
					tag.RawBytes = new byte[10 + length];
					Buffer.BlockCopy(buffer, 0, tag.RawBytes, 0, buffer.Length);
					Buffer.BlockCopy(remainder, 0, tag.RawBytes, 4, remainder.Length);

					ok = fillBuffer(stream, tag.RawBytes, 10, tag.RawBytes.Length - 10);
					if (!ok)
						return null;
					return tag;
				}

				// Check for a frame header, indicated by an 11-bit frame-sync
				// sequence.
				if (buffer[0] == 0xFF && (buffer[1] & 0xE0) == 0xE0)
				{
					var frame = new MP3Frame();

					var _ok = parseHeader(buffer, frame);
					if (_ok)
					{
						debug("NextObject: found frame");

						frame.RawBytes = new byte[frame.FrameLength];
						Buffer.BlockCopy(buffer, 0, frame.RawBytes, 0, buffer.Length);

						ok = fillBuffer(stream, frame.RawBytes, 4, frame.RawBytes.Length - 4);
						if (!ok)
							return null;
						return frame;
					}
				}

				// Nothing found. Shift the buffer forward by one byte and try again.
				debug("NextObject: sync error: skipping byte");
				buffer[0] = buffer[1];
				buffer[1] = buffer[2];
				buffer[2] = buffer[3];
                var n = stream.Read(buffer, buffer.Length - 1, 1);
                if (n < 1)
					return null;
			}
		}

		// parseHeader attempts to parse a slice of 4 bytes as a valid MP3 header. The
		// return value is a boolean indicating success. If the header is valid its
		// values are written into the supplied MP3Frame struct.
		public static bool parseHeader(byte[] header, MP3Frame frame)
		{
			// MPEG version. (2 bits)
			frame.MPEGVersion = (MPEGVersion)((header[1] & 0x18) >> 3);
			if (frame.MPEGVersion == MPEGVersion.MPEGVersionReserved)
			{
				return false;
			}

			// MPEG layer. (2 bits.)
			frame.MPEGLayer = (MPEGLayer)((header[1] & 0x06) >> 1);
			if (frame.MPEGLayer == MPEGLayer.MPEGLayerReserved)
			{
				return false;
			}

			// CRC (cyclic redundency check) protection. (1 bit.)
			frame.CrcProtection = (header[1] & 0x01) == 0x00;

			// Bit rate index. (4 bits.)
			var bitRateIndex = (header[2] & 0xF0) >> 4;
			if (bitRateIndex == 0 || bitRateIndex == 15)
			{
				return false;
			}

			// Bit rate.
			if (frame.MPEGVersion == MPEGVersion.MPEGVersion1)
			{
				switch (frame.MPEGLayer)
				{
					case MPEGLayer.MPEGLayerI:
						frame.BitRate = v1l1_br[bitRateIndex] * 1000;
						break;
					case MPEGLayer.MPEGLayerII:
						frame.BitRate = v1l2_br[bitRateIndex] * 1000;
						break;
					case MPEGLayer.MPEGLayerIII:
						frame.BitRate = v1l3_br[bitRateIndex] * 1000;
						break;
				}
			}
			else
			{
				switch (frame.MPEGLayer)
				{
					case MPEGLayer.MPEGLayerI:
						frame.BitRate = v2l1_br[bitRateIndex] * 1000;
						break;
					case MPEGLayer.MPEGLayerII:
						frame.BitRate = v2l2_br[bitRateIndex] * 1000;
						break;
					case MPEGLayer.MPEGLayerIII:
						frame.BitRate = v2l3_br[bitRateIndex] * 1000;
						break;
				}
			}

			// Sampling rate index. (2 bits.)
			var samplingRateIndex = (header[2] & 0x0C) >> 2;
			if (samplingRateIndex == 3)
			{
				return false;
			}

			// Sampling rate.
			switch (frame.MPEGVersion)
			{
				case MPEGVersion.MPEGVersion1:
					frame.SamplingRate = v1_sr[samplingRateIndex];
					break;
				case MPEGVersion.MPEGVersion2:
					frame.SamplingRate = v2_sr[samplingRateIndex];
					break;
				case MPEGVersion.MPEGVersion2_5:
					frame.SamplingRate = v25_sr[samplingRateIndex];
					break;
			}

			// Padding bit. (1 bit.)
			frame.PaddingBit = (header[2] & 0x02) == 0x02;

			// Private bit. (1 bit.)
			frame.PrivateBit = (header[2] & 0x01) == 0x01;

			// Channel mode. (2 bits.)
			frame.ChannelMode = (ChannelMode)((header[3] & 0xC0) >> 6);

			// Mode Extension. Valid only for Joint Stereo mode. (2 bits.)
			frame.ModeExtension = (byte)((header[3] & 0x30) >> 4);
			if (frame.ChannelMode != ChannelMode.JointStereo && frame.ModeExtension != 0)
			{
				return false;
			}

			// Copyright bit. (1 bit.)
			frame.CopyrightBit = (header[3] & 0x08) == 0x08;

			// Original bit. (1 bit.)
			frame.OriginalBit = (header[3] & 0x04) == 0x04;

			// Emphasis. (2 bits.)
			frame.Emphasis = (byte)(header[3] & 0x03);
			if (frame.Emphasis == 2)
			{
				return false;
			}

			// Number of samples in the frame. We need this to determine the frame size.
			if (frame.MPEGVersion == MPEGVersion.MPEGVersion1)
			{
				switch (frame.MPEGLayer)
				{
					case MPEGLayer.MPEGLayerI:
						frame.SampleCount = 384;
						break;
					case MPEGLayer.MPEGLayerII:
						frame.SampleCount = 1152;
						break;
					case MPEGLayer.MPEGLayerIII:
						frame.SampleCount = 1152;
						break;
				}
			}
			else
			{
				switch (frame.MPEGLayer)
				{
					case MPEGLayer.MPEGLayerI:
						frame.SampleCount = 384;
						break;
					case MPEGLayer.MPEGLayerII:
						frame.SampleCount = 1152;
						break;
					case MPEGLayer.MPEGLayerIII:
						frame.SampleCount = 576;
						break;
				}
			}

			// If the padding bit is set we add an extra 'slot' to the frame length.
			// A layer I slot is 4 bytes long; layer II and III slots are 1 byte long.
			var padding = 0;

			if (frame.PaddingBit)
			{
				if (frame.MPEGLayer == MPEGLayer.MPEGLayerI)
				{
					padding = 4;
				}
				else
				{
					padding = 1;
				}
			}

			// Calculate the frame length in bytes. There's a lot of confusion online
			// about how to do this and definitive documentation is hard to find as
			// the official MP3 specification is not publicly available. The
			// basic formula seems to boil down to:
			//
			//     bytes_per_sample = (bit_rate / sampling_rate) / 8
			//     frame_length = sample_count * bytes_per_sample + padding
			//
			// In practice we need to rearrange this formula to avoid rounding errors.
			//
			// I can't find any definitive statement on whether this length is
			// supposed to include the 4-byte header and the optional 2-byte CRC.
			// Experimentation on mp3 files captured from the wild indicates that it
			// includes the header at least.
			frame.FrameLength =
				(frame.SampleCount / 8) * frame.BitRate / frame.SamplingRate + padding;

			return true;
		}

		// getSideInfoSize returns the length in bytes of the side information section
		// of the supplied MP3 frame.
		public static int getSideInfoSize(MP3Frame frame)
		{
			var size = 0;
			if (frame.MPEGLayer == MPEGLayer.MPEGLayerIII)
			{
				if (frame.MPEGVersion == MPEGVersion.MPEGVersion1)
				{
					if (frame.ChannelMode == ChannelMode.Mono)
					{
						size = 17;
					}
					else
					{
						size = 32;
					}
				}
				else
				{
					if (frame.ChannelMode == ChannelMode.Mono)
					{
						size = 9;
					}
					else
					{
						size = 17;
					}
				}
			}
			return size;
		}

		// IsXingHeader returns true if the supplied frame is an Xing VBR header.
		public static bool IsXingHeader(MP3Frame frame)
		{
			// The Xing header begins directly after the side information block. We
			// also need to allow 4 bytes for the frame header.
			var size = getSideInfoSize(frame);

			if (frame.RawBytes.Length < 4 + size + 4)
			{
				return false;
			}

			var flag = new byte[4];
			Buffer.BlockCopy(frame.RawBytes, 4 + size, flag, 0, flag.Length);

			if (flag.SequenceEqual(Encoding.ASCII.GetBytes("Xing")) ||
				flag.SequenceEqual(Encoding.ASCII.GetBytes("Info")))
			{
				return true;
			}
			return false;
		}

		// IsVbriHeader returns true if the supplied frame is a Fraunhofer VBRI header.
		public static bool IsVbriHeader(MP3Frame frame)
		{

			// The VBRI header begins after a fixed 32-byte offset. We also need to
			// allow 4 bytes for the frame header.
			if (frame.RawBytes.Length < 4 + 32 + 4)
			{
				return false;
			}

			var flag = new byte[4];
			Buffer.BlockCopy(frame.RawBytes, 4 + 32, flag, 0, flag.Length);
			if (flag.SequenceEqual(Encoding.ASCII.GetBytes("VBRI")))
			{
				return true;
			}
			return false;
		}

		// NewXingHeader creates a new Xing header frame for a VBR file.
		public static MP3Frame NewXingHeader(UInt32 totalFrames, UInt32 totalBytes)
		{
			// We need a valid MP3 frame to use as a template. The data here is
			// arbitrary, taken from an MP3 file captured from the wild.
			var frame = new MP3Frame();
			frame.RawBytes = new byte[209];
			frame.RawBytes[0] = 0xFF;
			frame.RawBytes[1] = 0xFB;
			frame.RawBytes[2] = 0x52;
			frame.RawBytes[3] = 0xC0;
			var header = frame.RawBytes.Take(4).ToArray();
			parseHeader(header, frame);

			// Determine the Xing header offset.
			var offset = 4 + getSideInfoSize(frame);

			// Write the Xing header ID.
			var flag = Encoding.ASCII.GetBytes("Xing");
			Buffer.BlockCopy(flag, 0, frame.RawBytes, offset, flag.Length);

			// Write a flag indicating that the number-of-frames and number-of-bytes
			// fields are present.
			frame.RawBytes[offset + 7] = 3;

			// Write the number of frames as a 32-bit big endian integer.
			bigEndianPutUint32(frame.RawBytes, offset + 8, totalFrames);

			// Write the number of bytes as a 32-bit big endian integer.
			bigEndianPutUint32(frame.RawBytes, offset + 12, totalBytes);

			return frame;
		}

		// Print debugging information to stderr.
		public static void debug(string message)
		{
			if (DebugMode)
			{
				Console.WriteLine(message);
			}
		}

		// Attempt to read len(buffer) bytes from the input stream.  Returns a boolean
		// indicating success.
		public static bool fillBuffer(Stream stream, byte[] buffer, int offset, int count)
		{
			var n = stream.Read(buffer, offset, count);
			if (n < count)
				return false;
			return true;
		}

		public static void bigEndianPutUint32(byte[] buffer, int offset, UInt32 num)
		{
			var bytes = BitConverter.GetBytes(num);
			if (BitConverter.IsLittleEndian)
				bytes = bytes.Reverse().ToArray();
			Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
		}
	}
}
