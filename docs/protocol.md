# The update mirror protocol, as observed

This is what the tool implements and why. Everything here was observed from the
outside: the vendor's own mirror utility accepts a `--proxy` option, so pointing
it at a logging proxy shows its requests verbatim. No program was decompiled or
disassembled to write this.

The two product lines differ at almost every step. The one thing they share is
how a client is admitted.

## Admission: one header

The mirror serves files only to a client that sends

```
X-DrWeb-Validate: <MD5 of the license key file, 32 lowercase hex characters>
```

The value is the MD5 of the whole key file, byte for byte — not of its contents
normalised in any way. It is constant: the same value in every request, for every
URL, with no session, cookie or server-issued token anywhere in the exchange.

Measured, one header at a time, against a file on a live mirror:

| Sent | Answer |
| --- | --- |
| all headers the vendor tool sends | 200 |
| the same, with a plain `curl` user agent | 200 |
| without `X-DrWeb-Validate` | 451 |
| without `X-DrWeb-KeyNumber` | 200 |
| without `X-DrWeb-SysHash` | 200 |
| `X-DrWeb-Validate` alone | 200 |
| `X-DrWeb-KeyNumber` alone | 451 |
| `X-DrWeb-SysHash` alone | 451 |
| nothing | 451 |

The value is checked, not merely required: 32 random hex characters, or the MD5
of some other file, both give 451.

So a client needs the key file and an MD5 function, and nothing else. This tool
still sends the key number and a system hash, because the vendor counts requests
by them and there is no reason to hide; they do not affect admission.

One key opens both lines — the update line does not narrow admission.

## Transport

Plain HTTP, which is what the zone files themselves specify (`<protocol
value="http"/>`). The mirrors answer HTTPS too. Integrity does not come from the
transport: every file is pinned by SHA-256 and size in an index, and the index is
fetched from an address listed in the signed zone file that came from the machine
being updated.

The vendor signs its indexes. **Those signatures cannot be verified here** — the
public key is not published. That is a stated limit of this tool, not an
oversight.

## Zone files

A zone names one update line and the mirrors that serve it. It belongs to the
installed build of the antivirus, not to this tool, which is why nothing is built
in and the file is always taken from the machine being updated.

- **Linux:** `zone.ini`, an INI file where `url=` repeats — one primary entry
  under `[DRL]` and a dozen fallbacks under `[FALLBACK]`. The path part of the
  URL (`unix/<line>/<build>/vdb64`) is what says which bases these are.
- **Windows:** `drwzones.xml`, with `<path value="/xmlzone/…/windows"/>`, a
  `<geo-update>` server by name and numbered `<zone address=…>` mirrors.

Mirrors are tried in the order the file lists them, and the one that answers
serves the whole set: mixing mirrors inside one set could combine two different
releases. A 451 is final and is not retried elsewhere — every mirror would give
the same answer.

## The Linux line

One flat index, `version2.lst`:

```
[DrWebUpdateList]
[1788209114]                                       set timestamp, epoch seconds
+drw11000.vdb, <sha256>, <size>
+drweb32.dll, <sha256>, <size>, <sha256>, <size>    also offered as .lzma
-dwr900*.*,                                        delete locally
[DrWebUpdateListEnd]
[Signature]
Signature=…
```

Five fields mean a `<name>.lzma` exists next to the file; three mean it does not
(requesting it returns 404). Only the plain variant is fetched, because that is
what the vendor's own mirror tool stores, so no LZMA decoder is needed anywhere.

The resulting folder is the line directory (`vdb64`) holding `version2.lst` and
the files, which is exactly what `drweb-ctl update --from <folder>` expects.

## The Windows line

A tree of XML indexes, and **nothing is stored uncompressed** — the vendor mirror
holds `.lzma` files and `.xml` indexes, nothing else:

```
vdb-versions.xml                        entry point of the virus-bases group
  └─ <version>/vdb-revision.xml         the component indexes, with SHA-256
       ├─ 90/drwbase/9/revision.xml
       └─ 90/av-engine/9/revision.xml
            └─ <file name="common/…vdb" hash= size=>
                 <lzma name="common/…vdb.lzma" hash= size=/>
```

File paths inside a component index are relative to that index. The whole tree is
walked and verified before a single file is written: an interrupted walk would
otherwise leave something that looks like a mirror and is half of one.

Only the `vdb` group is mirrored. The `comps` group holds components of the
product itself, and updating the product is a different decision from updating
its bases — on a certified installation, usually a forbidden one.

The folder is named `Dr.Web Update Mirror`, as the vendor's client names it, so
the offline machine accepts it through its ordinary update-from-folder setting.

## Failure modes worth naming

- **451** — the license was not accepted. Not a network problem, and not worth
  retrying against other mirrors.
- **404 on the index** — the zone points at an update line that no longer exists.
  Take the zone file from the machine again.
- **An index that parses but lists nothing** — treated as a failure. An empty set
  that reports success is worse than an error: it is discovered on the machine
  that has no way to fetch anything.
