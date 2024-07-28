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

```bash
./RFDump dump --port COM3 --filename firmware.bin --baud-rate 115200 --chunk-size 0x1000
```