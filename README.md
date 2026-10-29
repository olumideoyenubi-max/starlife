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

## Starlife Town (GO OUT ✦)

**GO OUT ✦** in the main menu walks your starlet out of the suite into the web game's 3D town (the artifact's
`world.html`, Oct 2026). WASD or the arrows walk, Shift runs, drag to look, the wheel zooms, **E** interacts,
**M** opens the phone, **My suite** goes home.

- **The streets** are the web town's own. `node Tools/town/export.js` opens `Tools/town/world.html` in headless
  Chrome with `Tools/town/dump.js` injected, lets it build, and saves the finished scene merged per material and per
  48 m cell (`Resources/Town/towngeo.bytes`, about 315,000 triangles in 474 meshes), its 210 materials and 180 painted
  canvas textures (`Resources/Town/Tex`: facades, shopfronts, signs, posters, interiors, ground), and all gameplay data
  (`towndata.json`: 30 places, 118 seats, colliders, platforms, the walking graph, 293 street lamps, the activity,
  boutique, café and conversation tables). To take a newer version of the town, replace `Tools/town/world.html` with
  the artifact's and run the export again. `SLTownWorld.cs` builds it in Unity (`Resources/SLTown*.shader`: lit,
  see-through, sky and tone mapping) with the web town's day/night keyframes, sun, sky, fog, lamps and fountain.
- **The people** are the suite's 3D starlets (you, plus every other starlet with her avatar studio and salon
  settings). The pieces are baked meshes, so `Tools/blender/rig_export.py` saves each avatar's 22 main joints and her
  skin weights (`Resources/Rigs/<id>.bytes`), and `SLRig.cs` skins every piece to the joints of the avatar it was cut
  from (salon hairstyles follow the head). Walking, running, sitting and waving are the web town's procedural motion
  (`SLRigMotion`). The starlet meshes must stay Read/Write enabled for this.
- **The life** (`SLTown.cs`): routines and walking routes for the other starlets, places with opening hours and
  activities, chatting and friendships, hanging out and calling friends over, sitting on benches and terraces,
  Velvet Avenue's boutiques (bought pieces go into the suite's wardrobe in the brand colour through the core's
  `wearColor`; menswear is mapped to pieces the starlets can wear), Café Doré with coffee to go, the phone (map,
  places, friends, speed, skills) and waypoints. Coins, fame and clothes are the suite's; the clock, needs, skills,
  friendships and bought pieces are kept in the save under `host.town.*`.
- **Body.** Food eaten in town fills her out a little and exercise (jogging, running, the gym) slims and tones her,
  through the avatar studio's weight and muscle (`BodyChange` in `SLLife.cs`). The studio's Hips, Bust and the new Bum
  control go to 1.6 for curvier figures (`Reshape` in `StarletTint.shader`).
- **Check it:** `Builds/Mac/Starlife.app/Contents/MacOS/Starlife -sltown <dir> -screen-width 1440 -screen-height 900 -screen-fullscreen 0`
  plays a tour on its own save (`starlife-town-shots.json`) and writes about 30 screenshots.
- **Men.** Kai, Leo, Jin, Theo and Omar (the web game's men) are generated with MPFB by `Tools/blender/make_men.py`
  (run `Tools/blender/make_men.sh [ids]`: generate, cut into pieces, save the skeleton). Their bodies have their own
  shared body (`manbase`) and every item of the wardrobe maps to menswear made on that body (`m_suit`, `m_shirt`,
  `m_polo`, `m_hoodie`, `m_tee`, `m_casual`, `m_tweed`; `MenMap` in `SLWardrobe3D.cs`), so all of it fits all of them.
  The wardrobe and shop have a Menswear tab (shirt, chinos, suit trousers, suit, tuxedo, tie, bow tie, oxfords,
  loafers, cap). The avatar studio only offers men's faces and outfits to men, and women's to women.
- **Drawn (2D) mode** runs the October 2026 artifact script (`Tools/artifact-script.js`), which draws the men
  (male body, men's hair, beards) and the menswear. `core_api.js` skips its own starlets and items where the script
  has them, files the menswear under the Menswear tab, and keeps the Staff Polo (`staffpolo`, a work uniform) and
  Rainbow Hoodie (`rainbowhoodie`, gym) under their own ids.
- **The look of the men:** heads grow 15% about the neck (the starlets' Ready Player Me proportions), sleeves blend
  between arm and body by distance from the arm bones, and a corrective smooth irons out the shoulders once the arms
  are lowered (all in `make_men.py`).
- **Size:** starlet and hair models use Medium mesh compression (`SLModelImport.cs`) and the Mac build LZ4HC; starlet,
  hair, town and room textures are crunch-compressed. Car textures are not: the game reads their paint with `GetPixels`,
  which fails on crunched textures and stops the town from loading. `Builds/` is a symlink to
  `/Volumes/NHBuild/StarLife/Builds` (the internal disk is too full for a build).
- **Cars** (`SLTownCars.cs`): `Tools/blender/make_cars.py` turns the Generic passenger car pack (CC-BY) into ten models
  plus a limousine (`Resources/Cars`, wheels that turn and steer, paint recoloured in the game). Parked cars stand where
  the web town had its boxes (`export.js` drops those); traffic drives the road grid on the right, turns at junctions,
  keeps its distance, stops for people and for traffic lights (junctions of three or four roads: north-south and
  east-west take turns), and kerbs keep every car, yours too, off the pavements (the Avenue is car-free). Étoile Motors (north of Central Park's corner) sells
  them in ten colours after a free test drive; your car waits by your door, others are in the garage (the home card).
- **Clothes** (`Tools/blender/make_fashion.py`): 41 MPFB garments refitted onto the shared body (olivia), saved as
  `Assets/Starlets/f_*`: the arms and legs of an MPFB woman are posed onto olivia's joints, the garment is bound to her
  body and her body is shrink-wrapped onto olivia's. Most tops, trousers, dresses and flat shoes now use them
  (`SLWardrobe3D.Map`), and two collections were added: Ankara & Prints (prints kept) and New Season. Jackets and
  skirts are suits and dresses cut at the waist (`split`); sets in `PLAIN` get a plain twill instead of a print that
  would show through the dye. Loose skirt fabric is eased smooth after fitting, but never hem or seam vertices: moving
  those tears layered skirts apart. Flats, oxfords and loafers stay on the `cute` avatar's shoes (`toigo_flats` fits as
  a crumpled shell), and knee boots on `mary`'s.
- **Shops and restaurants you walk into** (`SLTownInteriors.cs`): the boutiques, jewellers, bookshop, cafés, bars,
  salon and spa, and Restaurant Row (eight eateries with storefronts on the block between the theatre and the Grand
  Hall: Greenwave Coffee, Big Bun Burgers, Sunny Side, Jollof & Co., Trattoria Bella, Sakura Omakase, The Gilded
  Steakhouse, Maison Lumière). Going in builds that one room out at `SLTownWorld.RoomX` (beyond the far clip, so only
  the room shows; its walls and counters join the town's colliders while you are inside) with fittings, seated guests
  and staff on the passers-by's bodies in dyed uniforms (`Look3.uniform`), aprons, caps and chef's hats. Talking to a
  cashier, host or sales assistant opens the place's own menu or rails; cooks, waiters and stock staff work their beat.
- **Directions:** the town is mirrored into Unity (`W` flips z), so a larger heading turns left on screen: walking
  uses `Atan2(-ix, iz)` and steering subtracts. The tour logs `walk right/left` and `steer right` checks in `town-log.txt`.
- **Fashion Week** (`SLTownFashion.cs`): at the Grand Hall in the evening, pick a theme, four hero pieces (the looks are
  completed from your wardrobe), and put on a runway show with starlets as models; three critics score it.
- **Passers-by** (`p_*`, also from `make_men.py`) walk the streets between all the places; they are not in the
  phone's friends list and do not stop to talk.
- **Controls:** keyboard and mouse; a game controller (left stick walk, right stick look, shoulder buttons or a stick
  press run, A use, B back, Y or Start phone, the d-pad moves through a card's options; the right stick reads the
  `Joy Axis` inputs added to the Input Manager); touch (drag on the left to walk with a joystick, on the right to
  look, pinch to zoom, tap a starlet to talk). Starlet and hair models must stay Read/Write (`SLModelImport.cs`).
- **Not ported:** the graphics-tier and FPS settings.

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
