# CsoundUnity

CsoundUnity brings the Csound audio engine into Unity. Use it to synthesize game audio, process live or recorded sound, and build interactive musical instruments. Write instruments and effects in `.csd` files, then control them from C#.

[![openupm](https://img.shields.io/npm/v/com.csound.csoundunity?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.csound.csoundunity/)
<!-- ALL-CONTRIBUTORS-BADGE:START - Do not remove or modify this section -->
[![All Contributors](https://img.shields.io/badge/all_contributors-14-orange.svg?style=flat-square)](#contributors-)
<!-- ALL-CONTRIBUTORS-BADGE:END -->

## Install CsoundUnity 3.5.2

In Unity, open **Window → Package Manager → + → Add package from git URL…** and paste the URL.

```text
https://github.com/rorywalsh/CsoundUnity.git
```

To install from disk, download the [3.5.2 source archive from the stable branch](https://github.com/rorywalsh/CsoundUnity/archive/refs/heads/master.zip) and extract it. In Package Manager, choose **Add package from disk…** and select the extracted `package.json`.

## Quick start

1. In the **Samples** tab for CsoundUnity, import **Table Morphing**.
2. Open the **Theremin** scene and enter Play Mode.
3. Move the mouse in the Game view. Move horizontally to change pitch and vertically to change volume.

Theremin uses Unity’s legacy Input Manager. If your project uses the new Input System, choose **Both** under **Edit → Project Settings → Player → Active Input Handling** to enable mouse control.

[CsoundUnity 3.5 documentation](https://rorywalsh.github.io/CsoundUnity/v3/)

## CsoundUnity 4.0 prerelease

See the [4.0.0-alpha.2 release notes and installation instructions](https://github.com/rorywalsh/CsoundUnity/releases/tag/v4.0.0-alpha.2) and the [4.0 documentation](https://rorywalsh.github.io/CsoundUnity/).

## Projects that use CsoundUnity

[Browse all projects and add yours](PROJECTS.md)

## Papers on CsoundUnity

[Browse papers and theses](PAPERS.md) on CsoundUnity and the projects built with it.

## License

CsoundUnity 3.5.2 ships with an [LGPL 2.1 license file](https://github.com/rorywalsh/CsoundUnity/blob/master/LICENSE).

CsoundUnity builds on Richard Henninger’s Csound6Net .NET wrapper.

## Contributors ✨

Thanks goes to these wonderful people ([emoji key](https://allcontributors.org/docs/en/emoji-key)):

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>
  <tbody>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="http://www.giovannibedetti.com"><img src="https://avatars1.githubusercontent.com/u/1912983?v=4?s=100" width="100px;" alt="Giovanni Bedetti"/><br /><sub><b>Giovanni Bedetti</b></sub></a><br /><a href="https://github.com/rorywalsh/CsoundUnity/commits?author=giovannibedetti" title="Code">💻</a> <a href="https://github.com/rorywalsh/CsoundUnity/issues?q=author%3Agiovannibedetti" title="Bug reports">🐛</a> <a href="#maintenance-giovannibedetti" title="Maintenance">🚧</a> <a href="#platform-giovannibedetti" title="Packaging/porting to new platform">📦</a> <a href="https://github.com/rorywalsh/CsoundUnity/commits?author=giovannibedetti" title="Documentation">📖</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/berntisak"><img src="https://avatars0.githubusercontent.com/u/9213871?v=4?s=100" width="100px;" alt="Bernt Isak Wærstad"/><br /><sub><b>Bernt Isak Wærstad</b></sub></a><br /><a href="https://github.com/rorywalsh/CsoundUnity/commits?author=berntisak" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://bermondo.com"><img src="https://avatars1.githubusercontent.com/u/33271566?v=4?s=100" width="100px;" alt="Charles Berman"/><br /><sub><b>Charles Berman</b></sub></a><br /><a href="https://github.com/rorywalsh/CsoundUnity/commits?author=ceberman" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://www.hcenteno.net"><img src="https://avatars3.githubusercontent.com/u/4064498?v=4?s=100" width="100px;" alt="Hector Centeno"/><br /><sub><b>Hector Centeno</b></sub></a><br /><a href="https://github.com/rorywalsh/CsoundUnity/commits?author=hectorC" title="Code">💻</a> <a href="#platform-hectorC" title="Packaging/porting to new platform">📦</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://npatsiouras.wordpress.com/"><img src="https://avatars.githubusercontent.com/u/6648226?v=4?s=100" width="100px;" alt="NPatch"/><br /><sub><b>NPatch</b></sub></a><br /><a href="https://github.com/rorywalsh/CsoundUnity/commits?author=NPatch" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://christopherpoovey.com"><img src="https://avatars.githubusercontent.com/u/20663797?v=4?s=100" width="100px;" alt="Chris Poovey"/><br /><sub><b>Chris Poovey</b></sub></a><br /><a href="https://github.com/rorywalsh/CsoundUnity/commits?author=composingcap" title="Documentation">📖</a> <a href="https://github.com/rorywalsh/CsoundUnity/issues?q=author%3Acomposingcap" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/nathanjams"><img src="https://avatars.githubusercontent.com/u/52339466?v=4?s=100" width="100px;" alt="nathanjams"/><br /><sub><b>nathanjams</b></sub></a><br /><a href="https://github.com/rorywalsh/CsoundUnity/issues?q=author%3Anathanjams" title="Bug reports">🐛</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Despair-Bear"><img src="https://avatars.githubusercontent.com/u/25762834?v=4?s=100" width="100px;" alt="Despair-Bear"/><br /><sub><b>Despair-Bear</b></sub></a><br /><a href="https://github.com/rorywalsh/CsoundUnity/commits?author=Despair-Bear" title="Code">💻</a> <a href="https://github.com/rorywalsh/CsoundUnity/issues?q=author%3ADespair-Bear" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/runette"><img src="https://avatars.githubusercontent.com/u/2239795?v=4?s=100" width="100px;" alt="Paul"/><br /><sub><b>Paul</b></sub></a><br /><a href="#tool-runette" title="Tools">🔧</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://reptile-sounddesign.com"><img src="https://avatars.githubusercontent.com/u/60217027?v=4?s=100" width="100px;" alt="Patrick J. Boettcher"/><br /><sub><b>Patrick J. Boettcher</b></sub></a><br /><a href="https://github.com/rorywalsh/CsoundUnity/issues?q=author%3Apatrickjboettcher" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/simogreg"><img src="https://avatars.githubusercontent.com/u/57944333?v=4?s=100" width="100px;" alt="simogreg"/><br /><sub><b>simogreg</b></sub></a><br /><a href="https://github.com/rorywalsh/CsoundUnity/issues?q=author%3Asimogreg" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://eris.codes"><img src="https://avatars.githubusercontent.com/u/3947103?v=4?s=100" width="100px;" alt="Eris Koleszar"/><br /><sub><b>Eris Koleszar</b></sub></a><br /><a href="https://github.com/rorywalsh/CsoundUnity/issues?q=author%3Abellicapax" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://makeplayhappy.com/"><img src="https://avatars.githubusercontent.com/u/46995545?v=4?s=100" width="100px;" alt="Makeplayhappy"/><br /><sub><b>Makeplayhappy</b></sub></a><br /><a href="https://github.com/rorywalsh/CsoundUnity/commits?author=makeplayhappy" title="Code">💻</a> <a href="#platform-makeplayhappy" title="Packaging/porting to new platform">📦</a> <a href="https://github.com/rorywalsh/CsoundUnity/issues?q=author%3Amakeplayhappy" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/horseisfish"><img src="https://avatars.githubusercontent.com/u/26128953?v=4?s=100" width="100px;" alt="horseisfish"/><br /><sub><b>horseisfish</b></sub></a><br /><a href="https://github.com/rorywalsh/CsoundUnity/commits?author=horseisfish" title="Code">💻</a></td>
    </tr>
  </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->

This project follows the [all-contributors](https://github.com/all-contributors/all-contributors) specification. Contributions of any kind welcome!
