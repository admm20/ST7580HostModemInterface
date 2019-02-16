using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace modem_com
{
    public enum Command
    {
        BIO_ResetRequest,
        MIB_WriteRequest,
        MIB_ReadRequest,
        MIB_EraseRequest,
        PingRequest,
        PHY_DataRequest,
        DL_DataRequest,
        SS_DataRequest
    }

    public enum Mode
    {
        PHY,
        DL
    }

    public enum Modulation
    {
        BPSK,
        QPSK,
        EIGHT_PSK,
        BPSK_WITH_PNA
    }

    class ModemRequest
    {
        public Command Cmd { get; set; }
        public string Name {
            get
            {
                return Cmd.ToString();
            }
        }

        public static byte[] CreateFrame(Command cmd, byte[] data, Mode mode,  Modulation modulation, bool FEC)
        {
            byte[] frame = null;
            byte conf = 0x01;
            byte[] tempData;
            switch (cmd)
            {
                case Command.BIO_ResetRequest:
                    frame = ModemCommunication.MakeFrame(0x3C, null);
                    break;
                case Command.MIB_WriteRequest:

                    if (mode.Equals(Mode.PHY))
                        frame = ModemCommunication.MakeFrame(0x08, new byte[] {0x00, 0x10 });
                    else
                        frame = ModemCommunication.MakeFrame(0x08, new byte[] {0x00, 0x11 });
                    
                    break;
                case Command.MIB_ReadRequest:
                    if(data.Length > 0)
                        frame = ModemCommunication.MakeFrame(0x0C, new byte[] { data[0] });
                        
                    break;
                case Command.MIB_EraseRequest:
                    if (data.Length > 0)
                        frame = ModemCommunication.MakeFrame(0x10, new byte[] { data[0] });
                    break;
                case Command.PingRequest:
                    if (data.Length > 0)
                        frame = ModemCommunication.MakeFrame(0x2C, data);
                    break;
                case Command.PHY_DataRequest:
                    if (modulation.Equals(Modulation.BPSK))
                        conf = 0x00;
                    else if (modulation.Equals(Modulation.QPSK))
                        conf <<= 4; // 0001 0000
                    else if (modulation.Equals(Modulation.EIGHT_PSK))
                        conf <<= 5; // 0010 0000
                    else
                        conf = 0x70; // 0111 0000

                    if(FEC)
                        conf |= 0x40; // 0100 0000
                    conf |= 0x04; // 0000 0100
                    tempData = new byte[data.Length + 1];
                    tempData[0] = conf;
                    for (int i = 0; i < data.Length; i++)
                    {
                        tempData[i + 1] = data[i];
                    }
                    if (data.Length > 0)
                        frame = ModemCommunication.MakeFrame(0x24, tempData);
                    break;
                case Command.DL_DataRequest:
                    if (modulation.Equals(Modulation.BPSK))
                        conf = 0x00;
                    else if (modulation.Equals(Modulation.QPSK))
                        conf <<= 4; // 0001 0000
                    else if (modulation.Equals(Modulation.EIGHT_PSK))
                        conf <<= 5; // 0010 0000
                    else
                        conf = 0x70; // 0111 0000

                    if (FEC)
                        conf |= 0x40; // 0100 0000
                    conf |= 0x04; // 0000 0100
                    tempData = new byte[data.Length + 1];
                    tempData[0] = conf;
                    for (int i = 0; i < data.Length; i++)
                    {
                        tempData[i + 1] = data[i];
                    }
                    if (data.Length > 0)
                        frame = ModemCommunication.MakeFrame(0x50, tempData);
                    break;
            }
            return frame;
        }

        public static Command GetCommandFromName(string CommandName)
        {
            Command command = Command.BIO_ResetRequest;
            switch (CommandName)
            {
                case "BIO_ResetRequest":
                    command = Command.BIO_ResetRequest;
                    break;
                case "MIB_WriteRequest":
                    command = Command.MIB_WriteRequest;
                    break;
                case "MIB_ReadRequest":
                    command = Command.MIB_ReadRequest;
                    break;
                case "MIB_EraseRequest":
                    command = Command.MIB_EraseRequest;
                    break;
                case "PingRequest":
                    command = Command.PingRequest;
                    break;
                case "PHY_DataRequest":
                    command = Command.PHY_DataRequest;
                    break;
                case "DL_DataRequest":
                    command = Command.DL_DataRequest;
                    break;
                case "SS_DataRequest":
                    command = Command.SS_DataRequest;
                    break;
            }
            return command;
        }

        public static List<ModemRequest> GetListOfCommands()
        {
            List<ModemRequest> cmdList = new List<ModemRequest>();
            
            //cmdList.Add(new ModemRequest(Command.BIO_ResetRequest));
            cmdList.Add(new ModemRequest(Command.MIB_WriteRequest));
            cmdList.Add(new ModemRequest(Command.MIB_ReadRequest));
            cmdList.Add(new ModemRequest(Command.MIB_EraseRequest));
            cmdList.Add(new ModemRequest(Command.PingRequest));
            cmdList.Add(new ModemRequest(Command.PHY_DataRequest));
            cmdList.Add(new ModemRequest(Command.DL_DataRequest));
            //cmdList.Add(new ModemRequest(Command.SS_DataRequest));

            return cmdList;
        }
        

        private ModemRequest(Command cmd)
        {
            this.Cmd = cmd;
        }
    }
}
