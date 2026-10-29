# Starlife (Unity)

Unity 6000.6.5f1 port of the "Starlife Suite" artifact: pick a starlet, dress her, do her hair and makeup,
decorate the suite, shop, enter style contests and save photos to an album.

## How it is put together

- `Assets/Resources/starlife_core.txt` is the artifact's own script (doll, clothes, makeup and furniture drawing,
  plus the wardrobe, shop and contest rules) with the web page parts removed. It is generated, not edited:
  `node Tools/build_core.js` rebuilds it from `Tools/artifact-script.js` and `Tools/core_api.js`.
- `SLCore.cs` runs that script inside Unity with Jint (`Assets/Plugins`) and stores the save.
- `SLSvg.cs` / `SLFilter.cs` draw the script's SVG output (paths, strokes, gradients, patterns, clip paths,
  blur, lighting and noise filters). `SLArt.cs` renders on worker threads and caches textures.
- `SLApp.cs` / `SLViews.cs` are the interface (UI Toolkit, styled by `Assets/Resources/starlife.uss`).

## 3D

The **3D** button in My Suite's toolbar (on by default, remembered between runs) switches the whole game between
the drawn dolls and 3D. In 3D there are 17 starlets, each with her own avatar; the suite is a real room; and the
picker, beauty parlor, fitting room, contests, home banner, round portraits and saved photos all show the avatars.
Drag the starlet sideways in My Suite to turn her.

- **Clothes.** Every avatar is cut into head, hair, body, top, bottom and shoes in one shared space, so pieces swap
  between starlets. With nothing on from the wardrobe a starlet wears her own outfit. As soon as she wears a top,
  bottom, dress, jacket or shoes she is rebuilt on a shared body, and each wardrobe item shows as the 3D piece
  `SLWardrobe3D.cs` maps it to, dyed to the item's colour. Glasses work the same way. Bags, jewellery and hats do not
  show in 3D yet, and several items share one 3D piece (there are three dress shapes, for example).
- **Hair Salon.** The HAIR SALON tab (3D only) shows the starlet in a salon and lists 15 African styles: nine made
  from scratch by `Tools/blender/make_hair.py` (box braids, twists, long locs, afro puffs, high puff, short natural,
  frohawk, Fulani braids with gold beads, braided bun) and six fitted from downloads (afro, curly afro, bantu knots,
  cornrows, short locs, loc ponytail), then every other starlet's hair. The colour swatches dye the style; beads and
  bands stay gold. The choice is remembered per starlet (PlayerPrefs `sl3d.hair.<id>`). Drag her, or use the Turn
  buttons, to see the back.
- **Underwear.** Wardrobe > Underwear picks a top (halter, bandeau, crop tank), a bottom (briefs, boy shorts) and a
  colour, and switches between "Signature outfit" and "Just underwear". The Undress button leaves her in underwear.
  The pieces are cut from the avatars' own close-fitting clothes by `Tools/blender/make_basics.py`.
- **Dragging.** A wardrobe piece can be dragged from the panel onto the room to wear it, and a shop piece onto the
  fitting room to try it on (`DragToWear` in `SLLife.cs`); a click still works. With the wardrobe open, a piece is
  pulled off by pressing on it on her figure and dragging it clear (`DragOff`); with it closed, dragging turns her.
- **Career.** The CAREER tab has seven star careers (the default: they are celebrities) and six everyday jobs. A star
  wears anything; on an everyday wage only pieces up to the job's budget, plus its uniform, are in the wardrobe and
  the shop, and the rest go into storage. "Go to work" pays by how well the outfit fits the job's dress code, three
  shifts a day. Jobs are the `Jobs` table in `SLLife.cs`.
- **Events.** The EVENTS tab (`SLEvents.cs`) has holidays (nine a year, each open for a few days around its date)
  and one get-together for every day of the week, all open without an invitation, plus three invitations a week
  that must be accepted beforehand and attended on the day; an accepted one that is missed costs 2 fame. Every
  event pays by how well her outfit fits its dress code. `evDate` lets the screenshot tour visit a holiday.
- **Gym.** The GYM tab (3D) shows her in a gym; in two or more pieces of sports gear with shoes she can do three
  workouts a day, each worth fame and one work shift back. Sports gear and workwear are extra wardrobe items
  declared at the top of `Tools/core_api.js` (drawn like an existing piece in the 2D game, own pieces in 3D).
- **Avatar studio.** Beauty Parlor > Avatar (3D) lets any starlet take another face and another arrival outfit, and
  sets her build with sliders: height, weight, muscle, shoulders, hips, bust, leg length and head size, with ready-made
  builds (petite, slim, tall model, athletic, curvy, bodybuilder, plus size). The build is applied in the shader
  (`Reshape` in `StarletTint.shader`) to every piece she is made of, so clothes change shape with the body. "My
  Starlet" in the picker is a blank one with a name of your choosing. These settings, the job and the underwear are
  stored in the save file through `SLCore.Pref`.
- **Sculpted faces** (`Tools/blender/face_sculpt.py`, run by `wardrobe_export.py`). Each head gets a ~278,000-triangle
  sculpt (cheekbones, brow, nose, lips, chin, jaw, slight asymmetry; smile lines, philtrum, lip line, under-eye; pores),
  baked to a 2048 px normal map and a cavity pass in the skin colour; the big forms also move the 17,400-triangle game
  head. The sculpts are in `Art3D/faces/<id>_sculpt.blend` for hand work; to use a hand-sculpted one, bake it over
  `Assets/Starlets/<id>/head_n.png`.
- **Makeup studio and salon sessions** (`SLSim.cs`). Beauty Parlor > Makeup (3D) is painted by hand on the mirror:
  a hidden render (`Resources/StarlifeUV.shader`) maps each mirror pixel to her skin texture, and strokes go into a
  1024 px makeup layer per starlet and face (`makeup-<starlet>-<face>.png` beside the save), which the head material
  blends over the skin. Picking a style in the Hair Salon starts a session: shampoo, rinse, blow-dry, comb out and
  style, each done by dragging over her until its bar fills.
- **Goth collection.** Wardrobe and Shop have a Goth tab: gown, trench, crop top, fishnet top, kilt with fishnets and
  a studded belt, two pairs of boots, and accessories that show in 3D (spiked collar and cuffs, cross pendant, choker,
  devil horns, cat ears, cross earrings, coffin bag). The accessories are made by `Tools/blender/make_goth.py`
  (source in `Art3D/goth.blend`). A rock star career wants goth or edgy pieces.
- **Not in 3D:** makeup, eye and brow choices (the avatars keep their own faces), wardrobe and furniture thumbnails,
  and the judges. Skin tone is approximated by tinting.
- **Look.** One shader (`Resources/StarletTint.shader`) draws everything: it dyes a piece by brightness while keeping
  trims, buttons and prints that are another colour, gives cloth and skin a soft edge light, and adds fine noise
  detail to hair. Pictures are drawn about 1.6 times larger than shown, with 8x anti-aliasing, and smoothed down.
  The made hairstyles use real plait and twist geometry (`braid` and `twist` in `make_hair.py`).
  Skin meshes are converted to quads and subdivided once on export (`wardrobe_export.py`), and the shader adds a
  red-tinted scattering term (`_SSS`). Three groomed styles (blowout afro, wash-and-go curls, silk press) follow the
  hair grooming workflow in code (`groom` in `make_hair.py`): guides on a scalp, interpolation, clumping, curl, frizz.
  `Art3D/hair/groomed_afro.blend` is a groom made in Blender's own hair system (Scalp object, Empty Hair curves,
  Interpolate / Clump / Curl / Frizz node groups): open it and use the hair sculpt brushes to restyle it.
  Garments are subdivided and given 3.5 mm of thickness on export; the thin originals stay in `pieces_thin.fbx`.
  Puffs, the high puff, the frohawk and the short crop are coiled strands too (`coil_ball`, `sleek`). Hair materials
  set `_Hair`, which adds highlights in bands across the strands, shading deep in the style, and streaks.
- **Room.** `SLStage3D.cs` builds the suite: Poly Haven floor materials with normal maps (`Resources/Room`), plaster
  relief on the painted walls, a capped skirting board, and wall pieces with real depth (a frame found from each drawn
  picture's outline; the window also has glazing bars, a sill, a rod and curtains). Sofa, armchair, rocking chair, side
  table, plant, coffee table and moving box are Poly Haven models (`Tools/blender/prop_export.py`); the lamps are lit;
  the rug is woven with a border and fringe. The floor lamp, gift box, salon and gym are still built from solids. The
  room camera puts a room point where the drawn room has it, so dragging and the rules work unchanged.

Bringing art in (all through Blender; `Tools/blender` must be on Blender's `sys.path`):

- Avatar: import a rigged Ready Player Me style model, `avatar_export.process('<id>', ..., bake=False)` saves it posed
  to `Art3D/avatars/<id>.blend`; then `blender -b Art3D/avatars/<id>.blend -P Tools/blender/wardrobe_export.py -- <id> "$PWD/Assets/Starlets"`
  cuts it into pieces. To make her a starlet, add her to the list at the top of `Tools/core_api.js` and run `node Tools/build_core.js`.
- Hairstyle, made: add a block to `Tools/blender/make_hair.py` and run
  `blender -b -P Tools/blender/make_hair.py -- "$PWD/Assets/Starlets/olivia/starlet.fbx" "$PWD/Assets/Hair" "$PWD/Art3D/hair" [style ids]`
  (previews land in `Art3D/hair/preview`); list its id in `Made` in `SLViews.cs` to show it first.
- Hairstyle, downloaded: with a starlet's head loaded as `REF_head`, `hair_fit.collect()`, `hair_fit.place(...)`, optionally
  `hair_fit.cap()` for styles that show the scalp, then `hair_fit.export('<id>', '<project>/Assets/Hair', '<project>/Art3D/hair')`.
- Furniture: `prop_export.process('<furniture kind>', <width in metres>, '<project>/Assets/Furniture', '<project>/Art3D/furniture')`.
- Editing a piece: every avatar, hairstyle and furniture model is kept as a `.blend` in `Art3D/`. Open it in Blender,
  change it, save, and run the matching export again (for an avatar, the `wardrobe_export.py` line above).
- Every build runs `SLBuild.Import3D`, which turns all of these into prefabs under `Resources` and writes `Resources/starlife3d.json`.
- The MCP for Blender add-on fetches Sketchfab and Poly Haven models once a Sketchfab API key is entered in its settings.
- Most of the art is CC-BY (see `Art3D/Credits.md`): credit the authors wherever the game is shared.

## Build and check

    Unity -batchmode -quit -projectPath ~/StarLife -executeMethod SLBuild.Setup      # once, creates the scene
    Unity -batchmode -quit -projectPath ~/StarLife -executeMethod SLBuild.BuildMac   # Builds/Mac/Starlife.app
    Builds/Mac/Starlife.app/Contents/MacOS/Starlife -slshots <dir> -screen-width 1440 -screen-height 900 -screen-fullscreen 0

`-slshots` plays through every screen on its own save file (`starlife-shots.json`), writes about 21 screenshots
(in 3D, with one drawn one) plus `avatar-<id>.png`, `hair-<id>.png`, `worn-*.png` and `spin-*.png` (outfits from four sides) checks of the avatars, hairstyles and wardrobe pieces, and quits. The real save is `starlife-save.json` in `~/Library/Application Support/Olumide Oyenubi/Starlife`.

Renderer checks without Unity: `node Tools/run_core.js` writes sample SVGs to `Tools/test`,
`Tools/chrome_ref.sh <name>` renders them with Chrome, the harness in `Tools/harness` renders them with `SLSvg`,
and `Tools/sidebyside.sh <name> <height>` puts the two next to each other.

## Not ported yet

Events and Chat, drag-and-drop dressing (click a piece instead), the custom colour picker, and text inside
drawings (the "Starlife" print on the tote, "MOVING CO." on the box).
