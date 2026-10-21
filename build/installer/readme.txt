CodeScope
=========

A native Windows desktop application for the scc code-analysis engine.

This installer sets up CodeScope on this computer. It installs the
application, the frozen analysis engine it bundles, and the documentation
that explains where the engine came from. A shortcut is added to the Start
menu, and the app can be removed again at any time through "Apps & features"
in Windows settings, or through the uninstall shortcut in the Start menu.

WHAT YOU NEED
-------------

CodeScope is published framework-dependent, so it needs the .NET 8 Desktop
Runtime (x64). Windows ships the .NET Framework, which is a different thing
and cannot run this application: the runtime has to be installed separately.

  https://dotnet.microsoft.com/download/dotnet/8.0

Choose Windows, x64, Runtime. It is a one-time installation of about a
minute. If the installer finds that the runtime is already there, it will
not say anything about it.

WHAT IT INSTALLS
----------------

  %LOCALAPPDATA%\Programs\CodeScope\     the application and the engine

  %LOCALAPPDATA%\CodeScope\              your settings and the analysis
                                          history, kept across updates and
                                          left alone when you uninstall

Nothing is written to the folders you analyse, and the application makes no
network requests.
