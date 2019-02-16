using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace modem_com
{
    public delegate void TerminalWrite(string text);

    

    class ModemCommunication
    {

        private enum LookingFor
        {
            BEGIN,
            LENGTH,
            CC,
            DATA,
            CHECKSUM_1,
            CHECKSUM_2
        }

        public SerialPort Port;
        public bool ApplicationRunning = true;
        public TerminalWrite TerminalOutput;

        private Queue<byte> rxDataQueue = new Queue<byte>();

        // bufor ramki o rozmiarze 170 bajtow
        private byte[] rxFrame = new byte[170];
        private int rxFrameIdx = 0;

        private LookingFor state = LookingFor.BEGIN;
        
        private int lengthChecker = 0;

        private Thread rxWorker;
        private Thread timerControl;
        

        public void ResetModem()
        {
            byte[] buffer = MakeFrame(0x3C, null); 
            foreach (byte b in buffer)
                Console.WriteLine(b);
            Port.RtsEnable = true;
            Thread.Sleep(10);
            Port.Write(buffer, 0, buffer.Length);
            Port.RtsEnable = false;

            // debugging
            string s = "";
            foreach (byte b in buffer)
            {
                s += string.Format("0x{0:X} ", b);
            }
            Console.WriteLine("wysylam: " + s);
        }

        public void SendFrame(byte[] frame)
        {
            Port.RtsEnable = true;
            Thread.Sleep(10);
            Port.Write(frame, 0, frame.Length);
            Port.RtsEnable = false;

            // debugging
            string s = "";
            foreach (byte b in frame)
            {
                s += string.Format("0x{0:X} ", b);
            }
            Console.WriteLine("wysylam: " + s);
        }

        public static byte[] MakeFrame(byte cmd, byte[] data)
        {
            int idx = 0;
            int data_length = (data != null) ? data.Length : 0;

            byte[] frame = new byte[5 + data_length];
            frame[idx++] = 0x02;
            frame[idx++] = (byte)data_length;
            frame[idx++] = cmd;

            if (data != null)
            {
                foreach (byte data_byte in data)
                {
                    frame[idx++] = data_byte;
                }
            }
            int checksum = 0;
            for (int i = 1; i < idx; i++)
                checksum += frame[i];

            frame[idx++] = (byte)(checksum & 0x00FF);
            frame[idx] = (byte)(checksum >> 8);

            return frame;
        }

        private void CheckQueue()
        {
            while (ApplicationRunning)
            {
                while (rxDataQueue.Count > 0)
                {
                    byte b = rxDataQueue.Dequeue();
                    BuildFrame(b);
                }

                Thread.Sleep(1);
            }
        }

        private long tim1 = 0;
        private void StartTimer1(long time)
        {
            tim1 = time;
        }

        private void StopTimer1()
        {
            tim1 = 0;
        }

        private void RefreshTimers()
        {
            while (ApplicationRunning)
            {
                if(tim1 > 0)
                {
                    tim1--;
                    if(tim1 == 0)
                    {
                        // ramka nie przyszla
                        Console.WriteLine("FRAME ERROR");
                        state = LookingFor.BEGIN;
                    }

                }

                Thread.Sleep(1);
            }
        }

        // ramka jest gotowa w rxFrame[] i ma rozmiar rxFrameIdx
        private void ReceiveFrame()
        {
            // pokazywanie samych danych
            if(rxFrame[0] == 0x02)
            {
                string tempMsg = "";
                //for (int i = 0; i < rxFrameIdx; i++)
                for (int i = 7; i < rxFrameIdx - 2; i++)
                {
                    //TerminalOutput(Byte2HexStr(rxFrame[i]));
                    tempMsg += (char)rxFrame[i];
                }
                tempMsg += '\n';
                TerminalOutput(tempMsg);

                // wysylanie ACK jezeli ramka zaczyna sie na 0x02
                byte[] ack = { 0x06 };
                Port.Write(ack, 0, 1);
            }
            
            state = LookingFor.BEGIN;
        }

        private string Byte2HexStr(byte b)
        {
            return string.Format("0x{0:X2} ", b);
        }

        private void BuildFrame(byte byteFromQueue)
        {
            if (state == LookingFor.BEGIN)
            {
                rxFrameIdx = 0;
                rxFrame[rxFrameIdx++] = byteFromQueue;

                if(rxFrame[0] != 0x06 && rxFrame[0] != 0x15 && 
                    rxFrame[0] != 0x02 && rxFrame[0] != 0x03 && 
                    rxFrame[0] != 0x3F)
                {
                    rxFrameIdx = 0;
                }
                else
                {
                    if (rxFrame[0] == 0x06 || rxFrame[0] == 0x15)
                    {
                        ReceiveFrame();
                    }
                    else if (rxFrame[0] == 0x02 || rxFrame[0] == 0x03 || rxFrame[0] == 0x3F)
                    {
                        state = LookingFor.LENGTH;
                        StartTimer1(10);
                    }
                }

            }
            else if(state == LookingFor.LENGTH)
            {
                rxFrame[rxFrameIdx++] = byteFromQueue;
                lengthChecker = byteFromQueue;
                if(rxFrame[0] == 0x3F)
                {
                    state = LookingFor.BEGIN;
                    StopTimer1();
                    ReceiveFrame();
                }
                else
                {
                    state = LookingFor.CC;
                    StartTimer1(10);
                }
            }
            else if(state == LookingFor.CC)
            {
                rxFrame[rxFrameIdx++] = byteFromQueue;
                if(rxFrame[1] != 0x00)
                    state = LookingFor.DATA;
                else
                    state = LookingFor.CHECKSUM_1;
                StartTimer1(10);
            }
            else if(state == LookingFor.DATA)
            {
                rxFrame[rxFrameIdx++] = byteFromQueue;
                lengthChecker--;
                StartTimer1(10);

                if(lengthChecker == 0)
                {
                    state = LookingFor.CHECKSUM_1;
                }
            }
            else if(state == LookingFor.CHECKSUM_1)
            {
                rxFrame[rxFrameIdx++] = byteFromQueue;
                StartTimer1(10);
                state = LookingFor.CHECKSUM_2;
            }
            else if(state == LookingFor.CHECKSUM_2)
            {
                rxFrame[rxFrameIdx++] = byteFromQueue;
                StopTimer1();

                // sprawdzanie sumy kontrolnej
                int sum = 0;
                for(int i = 1; i < rxFrameIdx - 2; i++)
                {
                    sum += rxFrame[i];
                }
                int checksum = rxFrame[rxFrameIdx - 1];
                checksum <<= 8;
                checksum |= rxFrame[rxFrameIdx - 2];
                if(sum == checksum)
                {
                    Console.WriteLine("Poprawna suma kontrolna: " + sum + "_" + checksum);
                    ReceiveFrame();
                }
                else
                {
                    Console.WriteLine("Niepoprawna suma kontrolna: " + sum + "_" + checksum);
                }
                state = LookingFor.BEGIN;
            }

            // w przypadku, gdy ktos bedzie probowal wyslac za dluga ramke, ma zresetowac caly proces
            if(rxFrameIdx > 168)
            {
                rxFrameIdx = 0;
                StartTimer1(1);
            }
        }

        private void Received(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort port = (SerialPort)sender;
            int dataLength = port.BytesToRead;
            byte[] data = new byte[dataLength];
            int nbrDataRead = port.Read(data, 0, dataLength);

            foreach (byte b in data)
            {
                rxDataQueue.Enqueue(b);
            }
        }

        public ModemCommunication()
        {
            Port = new SerialPort();

            Port.DataReceived += (new SerialDataReceivedEventHandler(Received));

            rxWorker = new Thread(CheckQueue);
            rxWorker.Start();

            timerControl = new Thread(RefreshTimers);
            timerControl.Start();
        }
    }
}
