# Starlife Atelier

A browser dress-up and makeup game inspired by classic virtual doll games. The whole game is one file, `index.html`. There is no build step and nothing to install.

## Run it

- **Locally:** download `index.html` and double-click it. It opens in any modern browser.
- **Local server (optional):** `python3 -m http.server`, then open http://localhost:8000.
- **GitHub Pages:** in the repo go to *Settings → Pages*, pick *Deploy from a branch*, choose your branch and the `/ (root)` folder, and save. The game will be live at `https://<user>.github.io/starlife/`.

## Features

- **Avatar:** skin tone (presets plus a custom colour picker), 4 eye shapes, eye colour, 6 brow styles with brow colour, 8 hairstyles, and hair colour.
- **Makeup Studio:**
  - Eyes: eyeshadow (5 styles, any colour, opacity), eyeliner (tightline, classic, winged, cat-eye, smudged) in any colour, mascara levels, and coloured contacts.
  - Lips: lipstick shade with matte, gloss or metallic finish and an intensity slider, plus lip liner.
  - Face: blush (colour, placement, intensity), highlighter (shade, glow) and contour.
  - One-click presets: Bare, Natural, Soft Glam, Night Out, Editorial.
- **Wardrobe:** tops, bottoms, dresses, outerwear, shoes and accessories (bags, necklaces, earrings, glasses, hats). Tap a piece or drag it onto the doll, and recolour anything you're wearing. Layering is handled for you:
  - A dress replaces the top and bottom.
  - Jackets go over everything.
  - Boots go over trousers.
  - A "Tuck top in" toggle sets whether the top or the bottom is on top.
- **Save & Showcase:** name a look and save it to the Lookbook. From there you can wear it again, download it as a PNG with a caption, or delete it. Your current look and your Lookbook are kept in the browser's localStorage.
