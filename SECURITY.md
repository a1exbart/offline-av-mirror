# Security

## Reporting a vulnerability

Report privately through GitHub's **Report a vulnerability** button on the
Security tab. Please do not open a public issue for something exploitable.

Expect an acknowledgement within a week. There is no bounty programme.

## What this program handles

**Your license key.** It is read from disk on the machine you run it on. Its
MD5 is sent to the vendor's mirror in the `X-DrWeb-Validate` header, because
that is how the protocol admits a client. The key itself is never sent, never
copied into the output, and never written to the console or a log. Treat the key
file as a password: anyone holding it can pull updates against your license.

**Downloaded files.** Every file is checked against the SHA-256 and size
declared by the mirror index before it gets its final name; a file that fails
is deleted. Paths coming from an index are rejected unless they stay inside the
output folder.

**What we cannot check.** The vendor signs its indexes and we have no public key
to verify those signatures with. The chain of trust is therefore: your own zone
file (signed, taken from your own machine) names the mirrors, the mirror serves
an index, and the index pins every file by hash. A mirror that is both hostile
and able to serve you a forged index could hand you the wrong bases. Plain HTTP
is what the zone files specify; the hashes, not the transport, are what protect
the content.

**Privileges.** The program never needs administrative rights. If something asks
you for them in its name, that is not this program.
