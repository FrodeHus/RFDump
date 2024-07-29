# Reliable Firmware Dump (RFDump)

RFDump is a firmware dump tool that can be used to extract firmware from devices such as routers, IP cameras, and more.  
It attempts to automate the extraction process and aims to be as reliable as possible by verifying the integrity of the data read from the device and re-reading data if necessary.

## Supported Devices

RFDump currently supports devices with the following boot loaders:

- U-Boot

## Usage

```bash
./RFDump --help
Usage:
  <command> [arguments]

Commands:
        ports
        dump


Global Arguments:
        --help  :       Show help information
        --output        :       Output format
```

### Ports

The `ports` command can be used to list the serial ports available on the system.

```bash
./RFDump ports
Available Ports: COM1, COM3
```

### Dump

The `dump` command can be used to extract firmware from a device.

Start the tool before plugging in the device to ensure that the tool can detect the device when it is connected and access the bootloader.

Arguments:

- `--port`: The serial port to connect to.
- `--filename`: The name of the file to save the firmware to.
- `--baud-rate`: The baud rate to use when connecting to the device.
- `--chunk-size`: The size of the chunks to read from the device. (Default: 16384).   
  - Smaller chunk sizes allow for quicker verification of the data read from the device and quicker recovery.  

```bash
.\RFDump.exe dump --port COM3 --filename firmware.bin

Dumping firmware from device connected to port COM3 at 115200 baud rate to firmware.bin with a chunk size of 4096 bytes
Bootloader detected: U-Boot

                          Access boot loader... ---------------------------------------- 100%
                       Gathering information... ---------------------------------------- 100%
Dumping memory 0xBC0A99C0/0xBD050000 (287Kb)... ----------------------------------------   2%
```

If the tool discovers that the data read from the device is incorrect, it will attempt to re-read the data. This will be indicated by the following message:

```text
Recovery in progress 0xBC087BA0/0xBD050000... ----------------------------------------   1%
``` 