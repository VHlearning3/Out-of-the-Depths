# Main menu background video

The main menu plays a looping video behind its title and buttons (the `VideoBackground` object under the Canvas in
`Scenes/MainMenu`, a `MenuBackgroundVideo` component).

To use one:

1. Put an **.mp4** (H.264) or **.webm** (VP8) file in this folder. Unity imports it as a Video Clip. GIFs cannot be
   played by Unity: convert a GIF to .mp4 first (any converter, or `ffmpeg -i in.gif out.mp4`).
2. Select `VideoBackground` in the MainMenu scene and drag the clip onto its **Clip** field. Play the scene.

That is the whole swap: change the Clip field to change the video. The other fields on the component:

- **Still Image**: a texture shown while the video loads and whenever there is no clip. With neither, the plain
  Background panel shows.
- **Cover Screen**: fill the screen and crop the edges (on), or fit inside it with bars (off).
- **Dim** / **Dim Color**: darkening over the picture so the text stays readable.
- **Loop**, **Speed**, **Play Audio**, **Volume**.
- **Streaming Assets File**: for a big video, put the file in `Assets/StreamingAssets` instead and type its name
  here (e.g. `menu.mp4`); it streams from disk rather than loading whole. Used only when Clip is empty.

Keep the video short and small (a 10 to 20 second loop at 1280x720 or 1920x1080 is plenty); it is a background, not
a film.
