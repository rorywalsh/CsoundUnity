# Cabbage 3

Open **Cabbage3.unity** and enter Play mode. 

**Cabbage3.csd writes its widgets as a JSON object inside `<Cabbage>`**, which is Cabbage 3's format
rather than Cabbage 2's lines of `widget bounds(...) channel(...)`. CsoundUnity reads both: it tells
them apart by what the section holds, since both wrap their widgets in the same tags. Every control
in the panel is a channel the parser found in that JSON, reached through the ordinary
`GetChannel`/`SetChannel` API.

## What this shows

- The top of the panel lists **what the parser made of the csd**: the widget names translated to the
  ones the rest of the package uses (`horizontalSlider` becomes `hslider`, `comboBox` becomes
  `combobox`), their channels and their ranges.
- **Low cut and high cut are two widgets**, not one. Cabbage 3 documents no two-handle slider.
- **A widget carries any number of channels**, each with its own interaction. The xyPad here
  declares two, and it is **the one widget read as a pair**, because they are the two directions of
  one drag, which the event names say. The X channel lands on `channel` and the Y on `channelY`.
  `GetChannel` and `SetChannel` work on either name, but `GetChannelController` looks names up among
  the first channels only, so the panel finds the second by scanning `channels` for a matching
  `channelY`.
- **Waveform counts from zero.** Cabbage 3 sends 0 for the first item of `items[]`, where Cabbage 2
  sends 1, and the orchestra in this csd reads the index the csd declared. The controller carries
  that convention in `zeroBasedOptions`, so nothing else in the package has to know.
- **Filter** switches the two filters off, so the band stops acting.

## What the console says, and when

This sample should open without warnings, because everything it declares has a control. A csd that
declares something this package does not act on is told about it, once:

- a widget there is no control for, such as `genTable`;
- a channel declared as text, which no numeric controller can hold;
- a channel written by a mouse interaction rather than by a value change, which arrives as a
  channel but is not driven from the pointer here;
- a key this version does not read, which matters because a misspelling is otherwise silent: the
  field keeps its default and the channel comes out plausible and wrong.

Each of those is said once per csd and not again, however many times the file is saved while the
scene is open. Changing the csd to something with a different limitation says the new one.

## Notes

The orchestra smooths the continuous channels with `portk` before using them. A control channel
steps once per control period, and a step in a gain or a filter cutoff is heard as zipper noise
while a slider moves: at a slider's own rate the discontinuity measured about twenty times the
signal's ordinary curvature, and about twice it once smoothed. The waveform and the filter switch
are left alone, because halfway between two states is not a state.

Rendering the csd outside Unity is silent, and that is correct: the orchestra reads everything from
channels, and only `Init()` writes the widget defaults into them. With those defaults the sample
peaks around a third of full scale.

Cabbage's own styling (`style`, colours, fonts) is in the csd and ignored: CsoundUnity does not draw
Cabbage's interface, it reads the channels behind it.
