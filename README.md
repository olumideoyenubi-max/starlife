# Starlife

A dress-up, makeup and room-decorating game in the spirit of the classic Stardoll suite. The whole game is one file, `index.html`. There is no build step and nothing to install.

## Run it

- **Locally:** download `index.html` and double-click it. It opens in any modern browser.
- **Local server (optional):** `python3 -m http.server`, then open http://localhost:8000.
- **GitHub Pages:** in the repo go to *Settings → Pages*, pick *Deploy from a branch*, choose your branch and the `/ (root)` folder, and save. The game will be live at `https://<user>.github.io/starlife/`.

## How it plays

- **Pick your starlet:** choose one of six made-up celebrities from the carousel. She moves into your suite in her underwear.
- **Your suite:** a furnished room with a wall, a wood floor, a city window, art and furniture. Furniture nearer the front overlaps her, so she can stand behind a table.
- **Guide:** Stella, your stylist, walks you through the basics in a speech bubble. Press **?** to see the tips again.
- **Wardrobe:** opens from the side menu. Drag clothes onto her, or tap to wear them. Drag a piece off her to take it off. Recolour anything she's wearing.
  - Layering is handled for you:
    - A dress replaces the top and bottom.
    - Jackets go over everything.
    - Boots go over trousers.
    - A "Tuck top in" toggle sets whether the top or the bottom is on top.
- **My Suite (decorating):**
  - Change the wall colour and the floor.
  - Add furniture: sofa, armchair, rocker, lamp table, monstera, glass table, rug, arc lamp, gift box, moving box, window, prints and a mirror.
  - Drag furniture and your starlet around the room. Things farther back get smaller.
- **Beauty Parlor:** a mirror with face and full-body views.
  - **Avatar:** skin tone, eye shape and colour, eyebrows, eight hairstyles, hair colour and underwear colour.
  - **Makeup Studio:** eyeshadow, eyeliner, mascara, coloured contacts, lipstick (matte, gloss or metallic) with lip liner, blush, highlighter and contour. One-click presets too.
- **Camera & My Album:** take a photo of the room and save it with a name. From My Album you can wear a look again, download it as a PNG, or delete it.
- **Toolbar:** Undress, Surprise me, what she's wearing (layer order), Undo, and Save.

Your current suite and your album are kept in the browser's localStorage.
