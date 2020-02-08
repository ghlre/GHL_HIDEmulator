using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.WinUsb;
using System;
using System.Collections.Generic;
using System.Text;
using Nefarius;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace GHL_HIDEmulator
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "GHL HID Emulator";
            UsbDevice guitar = null;
            foreach(WinUsbRegistry device in LibUsbDotNet.UsbDevice.AllWinUsbDevices)
            {
                // USB\VID_12BA&PID_074B
                if (device.Vid == 0x12BA && device.Pid == 0x074B)
                {
                    guitar = device.Device;
                }
            }
            if (guitar == null)
            {
                Console.WriteLine("Could not find any Guitar Hero Live guitars.");
                Console.WriteLine("Make sure you are using a PS3/Wii U Guitar Hero Live dongle with the WinUSB driver installed.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
                return;
            }

            // Set up Virtual Xbox 360 controller
            ViGEmClient client;
            try
            {
                client = new ViGEmClient();
            } catch (Exception)
            {
                Console.WriteLine("Failed to initialise ViGEm Client. Make sure you have the ViGEm bus driver installed.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
                return;
            }
            
            IXbox360Controller controller = client.CreateXbox360Controller();
            controller.Connect();
            Console.WriteLine($"Found a Guitar Hero Live guitar!");

            var reader = guitar.OpenEndpointReader(ReadEndpointID.Ep01);

            byte[] readBuffer = new byte[27];
            int runner = 0;
            while (true)
            {
                if (runner == 0)
                {
                    // Send control packet (to enable strumming)
                    byte[] buffer = new byte[9] { 0x02, 0x08, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                    int bytesWrote;
                    UsbSetupPacket setupPacket = new UsbSetupPacket(0x21, 0x09, 0x0201, 0x0000, 0x0008);
                    guitar.ControlTransfer(ref setupPacket, buffer, 0x0008, out bytesWrote);
                }
                runner++;
                if (runner > 500) runner = 0;

                int bytesRead;
                reader.Read(readBuffer, 100, out bytesRead);
                Console.SetCursorPosition(0, 1);
                Console.WriteLine("Frets:    " + BitConverter.ToString(new byte[] { readBuffer[0] }));
                Console.WriteLine("Buttons:  " + BitConverter.ToString(new byte[] { readBuffer[1] }));
                Console.WriteLine("Tilt:     " + BitConverter.ToString(new byte[] { readBuffer[19] }));
                Console.WriteLine("Whammy:   " + BitConverter.ToString(new byte[] { readBuffer[6] }));
                Console.WriteLine("Strum:    " + (readBuffer[4] == 0x00 || readBuffer[4] == 0xFF) + " ");
                Console.WriteLine("Raw Data: " + BitConverter.ToString(readBuffer));

                byte frets = readBuffer[0];
                controller.SetButtonState(Xbox360Button.A, (frets & 0x02) != 0x00); // B1
                controller.SetButtonState(Xbox360Button.B, (frets & 0x04) != 0x00); // B2
                controller.SetButtonState(Xbox360Button.Y, (frets & 0x08) != 0x00); // B3
                controller.SetButtonState(Xbox360Button.X, (frets & 0x01) != 0x00); // W1
                controller.SetButtonState(Xbox360Button.LeftShoulder, (frets & 0x10) != 0x00); // W2
                controller.SetButtonState(Xbox360Button.RightShoulder, (frets & 0x20) != 0x00); // W3

                byte strum = readBuffer[4];
                if (strum == 0xFF)
                {
                    // Strum Down
                    controller.SetButtonState(Xbox360Button.Down, true);
                    controller.SetButtonState(Xbox360Button.Up, false);
                } else if (strum == 0x00)
                {
                    // Strum Up
                    controller.SetButtonState(Xbox360Button.Down, false);
                    controller.SetButtonState(Xbox360Button.Up, true);
                } else
                {
                    // No Strum
                    controller.SetButtonState(Xbox360Button.Down, false);
                    controller.SetButtonState(Xbox360Button.Up, false);
                }

                byte buttons = readBuffer[1];
                controller.SetButtonState(Xbox360Button.Start, (buttons & 0x02) != 0x00); // Pause
                controller.SetButtonState(Xbox360Button.Back, (buttons & 0x01) != 0x00); // Hero Power

                // ViGEm isn't co-operating here - setting to some weird value causes an issue.
                //controller.SetAxisValue(Xbox360Axis.RightThumbY, (short)(~readBuffer[6]-128));
                //controller.SetAxisValue(Xbox360Axis.RightThumbX, readBuffer[19]);

                Console.WriteLine("Emulating as Xbox 360 Controller " + (controller.UserIndex + 1));
            }
        }
    }
}
