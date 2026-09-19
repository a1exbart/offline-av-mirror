# offline-av-mirror

Download antivirus virus bases on a machine that has internet access, carry them
to one that does not, and update it there.

*Русская версия: [README.ru.md](README.ru.md).*

One self-contained file for Windows and Linux, with a window for people who want
one and a command line for people who do not. Nothing to install on either
machine, and nothing left behind on the machine being updated.

It speaks the update protocol of **Dr.Web** products and produces the folder
layout their own tooling expects. *(Dr.Web is a trademark of Doctor Web, Ltd.
This project is not affiliated with, endorsed by, or supported by them. It
requires your own valid license and gives no access without one.)*

## Why

Machines in isolated networks still need fresh virus bases. The usual answer is
to run a mirror on a connected server; the usual reality is one engineer, one
USB drive and a machine behind an air gap. This tool is the short path: download
here, carry, update there.

## What it does

- Downloads the virus bases for the update line of **the machine you are
  updating** — Linux or Windows products.
- Writes them as a mirror folder in the vendor's own layout, so the offline
  machine takes them with its ordinary "update from a folder" procedure.
- Optionally packs the result into one archive with a `.sha256` beside it.
- Verifies every file against the checksum and size published by the mirror
  index, and keeps nothing if the download breaks.

## What it does not do

It does not install, update or configure an antivirus, does not change any
settings, and does not touch the antivirus on the machine it runs on. It
downloads files.

## Requirements

- A **valid license key file** (`drweb32.key`) of the machine being updated. The
  mirror admits a client by the checksum of that file; without it nothing is
  served.
- The **zone file** of that machine — it names the update line, which belongs to
  the installed build and cannot be guessed:
  - Linux: `/var/opt/drweb.com/drl/bases/zone.ini`
  - Windows: `drwzones.xml` from the product's data folder
- Internet access on the machine that runs the download.

Both are found without asking when they sit next to the program, and either can
be pointed at with the **Choose…** button in the window or with `--key` /
`--zone` on the command line. On Linux, if no zone file is next to the program,
the one of the antivirus installed on this machine is used and named as such —
convenient when you update the machine you are sitting at, but it is the wrong
line whenever the bases are meant for a different build, so check what the
window says.

## Usage

### Window

Run the program with no arguments. Choose the line (Linux or Windows), check the
key, the zone and the output folder, tick *pack into one archive* if the result
has to travel as a single file, and press **Download**.

### Command line

```console
$ offline-av-mirror download --out /media/usb --archive
offline-av-mirror 1.0.0
License:  No. 123456, valid until 2029-08-23
Zone:     unix/…/vdb64
Output:   /media/usb
  [1/46] drw11000.vdb
  …
Done: /media/usb/av-bases-unix-20260920-1530.tar.gz
258 MB · files in the set: 46
```

| Option | Meaning |
| --- | --- |
| `--out DIR` | where to put the result (default: current folder) |
| `--key FILE` | license key (default: `drweb32.key` next to the program) |
| `--zone FILE` | zone file from the machine being updated |
| `--line unix\|windows` | which line to look for when `--zone` is omitted |
| `--archive` | pack into one archive with a `.sha256` file |
| `--proxy HOST:PORT` | use an HTTP proxy |

Exit codes: `0` done, `1` failed, `2` wrong usage, `130` interrupted.

The interface language follows the system (English and Russian); set
`OFFLINE_AV_MIRROR_LANG=en` or `ru` to override it.

## The result

```
av-bases-unix-20260920-1530/        Linux line
└── vdb64/
    ├── version2.lst
    └── …

av-bases-windows-20260920-1530/     Windows line
└── Dr.Web Update Mirror/
    └── …
```

On the machine being updated:

- **Linux:** `drweb-ctl update --from /path/to/av-bases-unix-20260920-1530`
- **Windows:** point the product's update source at the extracted
  `Dr.Web Update Mirror` folder.

If you asked for an archive, verify it first: `sha256sum -c <archive>.sha256`.

### If the machine refuses the bases

Three answers from the vendor's tool are worth knowing in advance; all three were
seen on live machines.

- **"Operation is already in progress"** — the product keeps a flag that an update
  was started, and it survives the update that set it. Clear it and repeat:
  `drweb-ctl update --Stop`, then the same `update --from` again.
- **"Permission denied … Make sure that user drweb has access"** — the service
  runs as its own user and cannot read a folder inside someone's home directory.
  Put the folder somewhere readable (`/var/tmp`, a mounted drive) and give it
  `chmod -R a+rX`.
- **"Virus databases are not loaded"** from `drweb-ctl baseinfo` — that reports
  the state of the engine in memory, not of the disk. Right after an update it
  is normal and says nothing about the bases you just installed. What tells the
  truth is `/var/opt/drweb.com/bases/timestamp`: compare it before and after.

`Zone DWS skipped` in the output is expected: this tool mirrors the virus bases,
not the optional web-rating group.

## Limits worth knowing

- The vendor signs its indexes, and **this tool cannot verify those signatures** —
  the public key is not published. Integrity rests on the SHA-256 and size of
  every file, taken from an index fetched from an address in your own signed zone
  file.
- For the Windows line only the virus-bases group (`vdb`) is mirrored. Program
  components are deliberately left out: updating the product itself is a separate
  decision, and on a certified installation usually a forbidden one.
- The license key is read locally. Only its checksum is sent, as the protocol
  requires; the key itself is never copied into the result and never logged.

## Building from source

```console
$ dotnet publish src/OfflineAvMirror -c Release -r linux-x64   # or win-x64
```

.NET 10 SDK, no other prerequisites. Tests: `dotnet test`.

## License

[Apache 2.0](LICENSE). No warranty; see sections 7 and 8.
