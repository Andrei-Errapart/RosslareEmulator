using System;
using System.Collections.Generic;
using System.Text;

namespace RosslarEmulator
{
    public static class Utils
    {
        /// <summary>
        /// One character read from the serial port.
        /// </summary>
        public struct Reading
        {
            /// <summary>Read timestamp, milliseconds.</summary>
            public long timestamp_ms;
            /// <summary>Byte read.</summary>
            public byte reading;
            public Reading(byte reading)
            {
                this.timestamp_ms = TimestampMilliseconds();
                this.reading = reading;
            }


        };

        /// <summary>
        /// Timestamp, milliseconds.
        /// </summary>
        /// <returns>Timestamp, milliseconds.</returns>
        public static long TimestampMilliseconds()
        {
            return DateTime.Now.Ticks / (10 * 1000);
        }

        /// <summary>
        /// Is read queue equal to the given command?
        /// </summary>
        /// <param name="read_queue">Read queue.</param>
        /// <param name="command">Command.</param>
        /// <returns>True iff equal to the given command, false otherwise</returns>
        public static bool ReadingIsEqual(
            Queue<Utils.Reading> read_queue,
            byte[] command)
        {
            int n = read_queue.Count;
            if (n == command.Length)
            {
                int index = 0;
                bool equal = true;
                foreach (Reading r in read_queue)
                {
                    if (r.reading != command[index])
                    {
                        equal = false;
                        break;
                    }
                    ++index;
                }
                return equal;
            }
            return false;
        }

        /// <summary>
        /// Hex representation of data.
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="count">Number of items to use</param>
        /// <returns></returns>
        public static string hexstring_of(byte[] data, int count)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < count; ++i)
            {
                sb.Append(String.Format("{0:X2}", data[i]));
                if (i + 1 < count)
                {
                    sb.Append(' ');
                }
            }
            return sb.ToString();
        }
    }
}
