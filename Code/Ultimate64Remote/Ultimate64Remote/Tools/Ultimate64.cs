﻿using SchemaFactor;
using System;
using System.Net.Sockets;
using System.Windows.Forms;

namespace Ultimate64
{
    // Huge thanks to the JSIDPlay2 developers, this code is originally based on the following:
    // https://github.com/PotcFdk/JSIDPlay2/blob/master/jsidplay2/src/main/java/libsidplay/Ultimate64.java
    //
    // API Reference:
    // https://github.com/GideonZ/1541ultimate/blob/master/software/network/socket_dma.cc

    class Ultimate64Tools
    {
        public enum SocketCommand
        {
            // "Ok ok, use them then..."
            SOCKET_CMD_DMA = 0xFF01,
            SOCKET_CMD_DMARUN = 0xFF02,
            SOCKET_CMD_KEYB = 0xFF03,
            SOCKET_CMD_RESET = 0xFF04,
            SOCKET_CMD_WAIT = 0xFF05,
            SOCKET_CMD_DMAWRITE = 0xFF06,
            SOCKET_CMD_REUWRITE = 0xFF07,
            SOCKET_CMD_KERNALWRITE = 0xFF08,
            SOCKET_CMD_DMAJUMP = 0xFF09,
            SOCKET_CMD_MOUNT_IMG = 0xFF0A,
            SOCKET_CMD_RUN_IMG = 0xFF0B,

            // Undocumented, shall only be used by developers.
            SOCKET_CMD_LOADSIDCRT = 0xFF71,
            SOCKET_CMD_LOADBOOTCRT = 0xFF72,
            SOCKET_CMD_READMEM = 0xFF74,
            SOCKET_CMD_READFLASH = 0xFF75,
            SOCKET_CMD_DEBUG_REG = 0xFF76
        }

        static int SOCKET_RECEIVE_TIMEOUT = 1000;

        /**
         * Send Reset to Ultimate64.<BR>
         * 
         * @param config configuration
         */
        public static void SendReset(Config config)
        {
            SendCommand(config, SocketCommand.SOCKET_CMD_RESET);
        }

        public static void SendKeyboardKey(Config config, char key)
        {
            byte[] data = new byte[1];
            data[0] = (byte)key;
            SendCommand(config, SocketCommand.SOCKET_CMD_KEYB, data, false);
        }

        public static void SendKeyboardString(Config config, String Command)
        {
            byte[] data =  Utilities.GetBytesInverted(Command);
            SendCommand(config, SocketCommand.SOCKET_CMD_KEYB, data, false);
        }

        public static byte[] ReadMemory(Config config)
        {
            return SendCommand(config, SocketCommand.SOCKET_CMD_READMEM, null, true);
        }

        public static void WriteMemory(Config config, UInt16 address, byte[] data)
        {
            byte[] tosend = new byte[2 + data.Length];
            tosend[0] = Utilities.GetLowByte(address);
            tosend[1] = Utilities.GetHighByte(address);
            data.CopyTo(tosend, 2);

            SendCommand(config, SocketCommand.SOCKET_CMD_DMAWRITE, tosend, false);
        }

        private static void SendCommand(Config config, SocketCommand Command)
        {
            SendCommand(config, Command, null, false);
        }

        private static byte[] SendCommand(Config config, SocketCommand Command, byte[] data, bool WaitReply)
        {
            String hostname = config.Hostname;
            int port = config.Port;

            // Create a TCP/IP  socket.  
            Socket sender = new Socket(SocketType.Stream, ProtocolType.Tcp);

            if (data == null) data = new byte[0];
            byte[] reply = null;

            // Connect the socket to the remote endpoint. Catch any errors.  
            try
            {
                sender.Connect(hostname, port);

                byte[] ram = new byte[4 + data.Length];
                ram[0] = Utilities.GetLowByte((UInt16)Command);
                ram[1] = Utilities.GetHighByte((UInt16)Command);
                ram[2] = Utilities.GetLowByte((UInt16)data.Length);
                ram[3] = Utilities.GetHighByte((UInt16)data.Length);
                Array.Copy(data, 0, ram, 4, data.Length);

                // Send the data through the socket.  
                int bytesSent = sender.Send(ram);

                // Wait for a reply?
                if (WaitReply)
                {
                    reply = GetReply(sender);
                }

                // Release the socket.  
                sender.Shutdown(SocketShutdown.Both);
                sender.Close();
                return reply;
            }
            catch (Exception e)
            {
                MessageBox.Show("Ultimate64: Cannot send command: " + e.Message);
                return new byte[0];
            }
        }

        private static byte[] GetReply(Socket sock)
        {
            byte[] buffer = new byte[0x800000];   // Yep.  As in socket_dma.cc line 140

            try
            {
                sock.ReceiveTimeout = SOCKET_RECEIVE_TIMEOUT;
                buffer = ReceiveMessage(sock, buffer.Length);
                return buffer;
            }
            catch (Exception e)
            {
                MessageBox.Show("Ultimate64: Cannot receive reply: " + e.Message);
                return new byte[0];
            }
        }

        private static byte[] ReceiveMessage(Socket socket, int messageSize)
        {
            int BufferSize = 1024;

            byte[] messageBuffer = new byte[messageSize];

            int bytesReceived = 0;
            int totalBytesReceived = 0;
            do
            {
                byte[] buffer = new byte[BufferSize];

                // Receive at most the requested number of bytes, or the amount the 
                // buffer can hold, whichever is smaller.
                int toReceive = Math.Min(messageSize - totalBytesReceived, BufferSize);
                bytesReceived = socket.Receive(buffer, toReceive, SocketFlags.None);

                // Copy the receive buffer into the message buffer, appending after 
                // previously received data (totalBytesReceived).
                Buffer.BlockCopy(buffer, 0, messageBuffer, totalBytesReceived, bytesReceived);

                totalBytesReceived += bytesReceived;

                if (totalBytesReceived == messageSize) break;

                Console.WriteLine("Received: " + totalBytesReceived);

            } while (bytesReceived > 0);

            if (totalBytesReceived < messageSize)
            {
                throw new Exception("Server closed connection prematurely");
            }

            return messageBuffer;
        }
    }
}
