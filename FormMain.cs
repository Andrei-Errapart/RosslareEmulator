using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace RosslarEmulator
{
    public partial class FormMain : Form
    {
        private delegate void SetTextDelegate();
        /// <summary>
        /// Log a line of text.
        /// </summary>
        /// <param name="source">Textbox with the serial port name.</param>
        /// <param name="text">Text to be logged.</param>
        private void log(string name, string text)
        {
            string s = DateTime.Now.ToString() + ":" + name + ":" + text + "\r\n";
            if (textBoxLog.InvokeRequired)
            {
                Invoke(new SetTextDelegate(delegate() { textBoxLog.AppendText(s); }));
            }
            else
            {
                textBoxLog.AppendText(s);
            }
        }

        public FormMain()
        {
            InitializeComponent();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            // 1. Load the configuration file.
            CSUtils.FileConfig cfg = new CSUtils.FileConfig("RosslarEmulator.ini");
            try
            {
                cfg.Load();
                string portspec = cfg.Fetch("RosslarEmulator", "SerialPort", CSUtils.FileConfig.OPT.THROW_WHEN_NOT_FOUND, null);
                CSUtils.SerialPortHelper.openSerialPortByDescription(serialPortRosslar, portspec);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error");
                Application.Exit();
            }
        }

        /// <summary>Read queue. Used only in serialPortRosslar_DataReceived.</summary>
        private Queue<Utils.Reading> rqueue = new Queue<Utils.Reading>();
        private static byte[] query_door1 = { 0xFF, 0xFF, 0xFF, 0x55, 0x02, 0x01, 0x02, 0x51, 0x00, 0x01, 0x41, 0x98 };
        private static byte[] query_door2 = { 0xFF, 0xFF, 0xFF, 0x55, 0x02, 0x02, 0x02, 0x51, 0x00, 0x01, 0x41, 0x99 };

        private void serialPortRosslar_WriteReply(byte[] reply)
        {
            log("Write", Utils.hexstring_of(reply, reply.Length));
            serialPortRosslar.Write(reply, 0, reply.Length);
        }

        /// <summary>
        /// Output queue. Key is the time for output, in milliseconds, derived from DateTime.
        /// </summary>
        private SortedDictionary<long, byte[]> outputQueue = new SortedDictionary<long, byte[]>();

        private void serialPortRosslar_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            // 1. flush old data from the input queue.
            {
                long time_out = Utils.TimestampMilliseconds() - 1000;
                int timeout_count = 0;
                int previous_count = rqueue.Count;
                Queue<byte> purged_bytes = new Queue<byte>();
                for (; rqueue.Count > 0 && rqueue.Peek().timestamp_ms < time_out; )
                {
                    Utils.Reading r = rqueue.Dequeue();
                    purged_bytes.Enqueue(r.reading);
                    ++timeout_count;
                }
                if (timeout_count > 0)
                {
                    log("Timeout", String.Format("Purged {0} bytes.", timeout_count));
                }
            }

            // 2. fetch new data.
            byte[] rbuffer = new byte[2048];
            int nread = serialPortRosslar.Read(rbuffer, 0, rbuffer.Length);
            if (nread > 0)
            {
                log("Read", Utils.hexstring_of(rbuffer, nread));
                foreach (byte b in rbuffer)
                {
                    rqueue.Enqueue(new Utils.Reading(b));
                    if (Utils.ReadingIsEqual(rqueue, new byte[] { 0x0D, 0x0A }))
                    {
                        log("Read", "CRLF");
                        rqueue.Clear();
                    }
                    else if (Utils.ReadingIsEqual(rqueue, query_door1))
                    {
                        log("Read", "Query door 1");
                        rqueue.Clear();
                        long time_output = Utils.TimestampMilliseconds() + 500;
                        byte[] reply = new byte[] {
                            0xFF, 0xFF, 0x55, 0x02, 0x51, 0x02, 0x01, 0x00, 0x0B, 0xC0, 0x00, 0x32, 0x16, 0x0A, 0x0F, 0x08, 0x08, 0x05, 0x18, 0xE2, 0x91, 0x00
                        };
                        lock (outputQueue)
                        {
                            if (!outputQueue.ContainsKey(time_output))
                            {
                                outputQueue.Add(time_output, reply);
                            }
                        }
                    }
                    else if (Utils.ReadingIsEqual(rqueue, query_door2))
                    {
                        log("Read", "Query door 2");
                        rqueue.Clear();
                    }
                }
            }
            else
            {
                log("Error", String.Format("Read error, return value {0}", nread));
            }
        }

        private void buttonClearLog_Click(object sender, EventArgs e)
        {
            textBoxLog.Text = "";
        }

        /// <summary>
        /// Only for use in timerOutput_Tick.
        /// </summary>
        private Queue<long> keys_to_remove_ = new Queue<long>();

        private void timerOutput_Tick(object sender, EventArgs e)
        {
            long time_now = Utils.TimestampMilliseconds();

            lock (outputQueue)
            {
                foreach (KeyValuePair<long, byte[]> kv in outputQueue)
                {
                    if (kv.Key < time_now)
                    {
                        byte[] reply = kv.Value;
                        keys_to_remove_.Enqueue(kv.Key);
                        serialPortRosslar.Write(reply, 0, reply.Length);
                        log("Write", Utils.hexstring_of(reply, reply.Length));
                    }
                }

                foreach (long k in keys_to_remove_)
                {
                    outputQueue.Remove(k);
                }
            }
            keys_to_remove_.Clear();
        }
    }
}