# Starlife

A dress-up, makeup and room-decorating game in the spirit of the classic Stardoll suite. The whole game is one file, `index.html`. There is no build step and nothing to install.

## Run it

- **Locally:** download `index.html` and double-click it. It opens in any modern browser.
- **Local server (optional):** `python3 -m http.server`, then open http://localhost:8000.
- **GitHub Pages:** in the repo go to *Settings → Pages*, pick *Deploy from a branch*, choose your branch and the `/ (root)` folder, and save. The game will be live at `https://<user>.github.io/starlife/`.

## How it plays

- **What's New:** a group photo of all the starlets, your daily coin bonus, today's contest, new arrivals in the Shop, and "Starlets you might like".
- **Top bar:** your starlet's avatar, name and level, plus your fame and coin balances. Saving looks, contests and shopping earn fame, and every 60 fame is a new level.
- **Shop:** some clothes cost coins. Try them on in the fitting room, then buy them and they appear in her wardrobe.
- **Contests:** five themed style challenges (Red Carpet Premiere, Beach Day, Office Chic, Snow Day Stroll, Rock Concert). A live checklist shows what the judges look for. Three judges score her look out of 100, give stars and comment. You win coins and fame once per contest per day, plus more if you beat your best score.

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
- **Events (social life):** RSVP to invitations at eleven venues:
  - Book Club Reading, Coffee Date, Rooftop Lounge, Club Night, House Party, Garden Party, Masquerade Ball, Charity Banquet, The Grand Gala, the Starlife Awards red carpet, and a Fashion Week show.
  - Each has its own illustrated scene, a dress code and guests from the other starlets.
  - Get ready in your wardrobe, then play three story moments where you choose what she does.
  - You're rated on your outfit and earn a title, coins and fame. You can snap the scene for your album.
  - House parties take place in your own suite, and your decorating counts.
- **Chat:**
  - **Starlets and Stella:** private chats with the starlets and your stylist. They also message you after events. When the game is hosted as a Claude artifact, their replies are written by Claude in character. Otherwise they use built-in replies.
  - **The Lounge:** a live chat room for everyone who has the hosted page open at the same time. Lounge messages aren't saved.
- **Toolbar:** Undress, Surprise me, what she's wearing (layer order), Undo, and Save.

Your current suite, your album and your coins are kept in the browser's localStorage.

Starlife is an independent fan project. It isn't affiliated with Stardoll and uses none of its code or artwork.

## Life mode (Sims-style)

Your starlet now lives in their suite:

- **Needs.** Hunger, energy, hygiene, fun and social slowly drop as the clock runs. Mood comes from the needs, and the star above your starlet's head shows it (green, yellow or red).
- **Clock.** Use the pause, play, fast and faster buttons in the life bar. Days run Monday to Sunday. The room gets darker in the evening.
- **Actions.** Click furniture to use it. You can cook or snack in the kitchen, sleep or nap in bed, shower, watch TV, read at the bookshelf, practise at the desk, run on the treadmill or dance by the stereo. Click your starlet to call or text friends. Buy more furniture from the Decor drawer.
- **Skills.** Cooking, fitness, charisma and creativity grow as you use the matching furniture.
- **Career.** Take a job from the Life panel and go to work on weekday mornings. Your pay depends on your mood and how well your outfit suits the office. Three great days in a row earn a promotion.
- **Friends.** Messaging starlets and going to events builds friendships, from Stranger up to Best friend.

## Male starlets

Kai, Leo, Jin, Theo and Omar join the cast. In the Avatar tab you can switch any starlet's body, and pick beards and men's hairstyles: crop, buzz, quiff and locs. The Shop sells menswear: shirt, polo, hoodie, chinos, suit trousers, suit, tuxedo, tie, bow tie, oxfords, loafers and cap.

## Starlife Town (3D open world)

Click **Go out ✦** in the main menu, or open `world.html`, to walk around a 3D town as your starlet. It is built with three.js, which loads from a CDN, so the page needs an internet connection. Your starlet keeps the outfit, skin, hair and beard you chose in the suite.

- **Explore freely.** Use WASD or the arrow keys to walk, Shift to run, and drag to turn the camera. On a phone, drag on the left to walk and on the right to look.
- **Places:** Starlife Suites (your home), the Bean & Bloom café and its terrace, the Chapter One bookshop, the Maison Starlife boutique, the Neon Owl club, the Fizz mocktail bar, the Grand Hall, Starlight Theatre, Maya's house and Central Park with its fountain. Walk up to a door and press **E** to see what you can do there. Some places only open at certain hours, and the gala events reward a dressy look.
- **Velvet Avenue**, a car-free luxury street west of your home. It has lamp-lit planters, benches, string lights and an arch at each end. Along it are:
  - **Shops:** Aurelle (fine jewellery), Maison Velours (couture), Noir & Or (leather goods), Lumière (perfume) and Celestine (watches and diamonds). Their windows have mannequins in gowns. Buying a signature piece costs coins and earns fame.
  - **Places to meet:** the Petit Four patisserie terrace, the Sorbetto gelato parlour, the Sky Garden rooftop bar, Serenity Spa and The Velvet Room members' lounge.
- **Starlife Galleria**, a mall you can walk through. Two levels of shops line a glass-roofed walkway around an atrium with a tree and a food court. Inside are Glimmer, Sneaker Vault, Glam Salon (opens the Beauty Parlor), Candy Cloud, the Play Zone arcade and Star Cinema. The entrance sign lists every store and guides you to the one you pick.
- **Get-togethers:** at a hangout spot (café, bar, park, Maya's house, the Avenue cafés and lounges, or the mall's food court, cinema and arcade), choose **Call your friends to meet here**. Your closest friends walk over, and once they arrive you can **Hang out** together for a big boost to social and fun, and your friendships grow.
- **Starlets live in town.** The other starlets walk between their favourite places on a daily routine, sit on the café terrace and park benches, and go home late at night. Walk up to one, or tap them, to chat, pay them a compliment or ask them to hang out. Once you're friends, they'll follow you to your next activity.
- **One shared life.** The clock, needs, skills, friendships and coins are the same ones the suite uses. Day turns into night, and the street lamps and windows light up after dark.
- **Phone (M):** a town map you can set routes on, a list of where your friends are, and settings for graphics quality (Low, Medium, High), time speed and an FPS counter.

Graphics tiers: **Low** (the default on phones) has no shadows, a shorter view distance and a smaller crowd. **Medium** adds soft shadows, sky lighting and more people. **High** adds anti-aliasing, sharper shadows and the longest view distance. Building interiors are not walkable: going inside a venue plays the activity as a short scene.
