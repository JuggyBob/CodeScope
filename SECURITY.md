# Security policy

## Reporting a vulnerability

**Please do not open a public issue for a security problem.**

Report it privately through GitHub's
[private vulnerability reporting](https://docs.github.com/en/code-security/security-advisories/guidelines/reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)
form: open the repository's **Security** tab and choose **Report a
vulnerability**. If the repository does not show that option, open a security
advisory through the API or contact a maintainer directly using the address on
the repository page.

Please include:

* what the problem is and what an attacker could do with it;
* the version or commit you found it in;
* the steps to reproduce it;
* any proof-of-concept material, if you have it.

You will get an acknowledgement within **seven days**. You will get a status
update at least every **seven days** while it is being looked at, and a fix or
an explanation when the matter is closed. If a fix is needed we will agree the
timing and credit you in the release notes unless you would rather we did not.

If we cannot reproduce the problem or do not consider it a vulnerability, we
will say so plainly and explain why.

## What to report

Anything that could:

* make the application run code it should not, or read or write files it
  should not;
* let one analysed project affect another;
* leak the contents of an analysed codebase, of the history store or of the
  settings file;
* let a crafted repository make the application misbehave — a crash loop, a
  hang, unbounded memory use or unbounded disk use;
* let a crafted repository run a command that was not intended (the application
  never passes a shell string to the engine, and this is worth reporting if it
  ever changes);
* undermine the guarantee that the in-app report writes nothing to the analysed
  folder.

## What not to report

* Bugs that need a local, already-compromised machine to exploit.
* Vulnerabilities in the upstream scc engine as such. Those belong with the
  upstream project; report them there. If the snapshot in this repository
  carries a known issue, a note here is welcome.
* Vulnerabilities in the .NET runtime or WPF — those belong with Microsoft.
* Missing hardening, or a defence-in-depth suggestion with no demonstrated
  impact. Suggest those as ordinary issues.

## Supported versions

| Version | Supported |
| --- | --- |
| 1.0.x | Yes |
| 1.0.0 | Yes — the first public release |
| < 1.0.0 | No |

Fixes land on the latest minor release. There has never been a release before
1.0.0, so there is nothing older to patch; if an issue is found in 1.0.0 it
will be fixed in 1.0.1.

## What the application does and does not do

These are the properties the security work is built on. If any of them stops
being true, that is a security bug.

* The application makes **no network request** of any kind. It cannot phone
  home because it has no code that can.
* The **in-app report writes nothing** — not to the analysed folder, not
  anywhere else. The HTML export writes exactly one `.html` file, to a folder
  the user chooses, and never overwrites an existing file without an explicit
  confirmation.
* The analysed codebase is read only. Nothing in the application opens a
  handle for writing inside the analysed folder.
* The bundled engine is a **byte-for-byte snapshot** of an upstream commit,
  verified by `vendor/scc-source/SNAPSHOT-SUMS.sha256`. No upstream file is
  modified.
* Settings and the analysis history live in `%LOCALAPPDATA%\CodeScope`, one
  file per run, capped at thirty runs.
* Engine arguments are passed as an argument list, never as a shell string, so
  a path with spaces, quotes or unusual characters cannot be re-parsed.
