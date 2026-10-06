"""Build the extended, paired Hungarian vocabulary pack.

Each tuple is a headword and its English meaning. Topic-specific examples are
stored alongside the generated cards so the app never builds sentences at run
time. Run with the workspace Python to refresh Content/expanded-cards.json.
"""

from __future__ import annotations

import json
import re
import unicodedata
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Content" / "expanded-cards.json"


WORDS: dict[str, list[tuple[str, str]]] = {
    "Street talk": [
        ("mi újság", "what's new"), ("mizu", "what's up (very casual)"), ("köszi", "thanks (casual)"),
        ("bocsi", "sorry (casual)"), ("csá", "bye (casual)"), ("hellóka", "hi there (playful)"),
        ("tök jó", "really cool"), ("menő", "cool / stylish"), ("király", "awesome (slang)"),
        ("szuper", "great"), ("gáz", "awkward / bad (slang)"), ("para", "creepy / stressful (slang)"),
        ("haver", "buddy"), ("csaj", "girl / young woman (casual)"), ("pasi", "guy (casual)"),
        ("buli", "party"), ("pia", "booze (slang)"), ("kaja", "food (casual)"),
        ("laza", "relaxed / chill"), ("tuti", "for sure (slang)"), ("simán", "easily / sure"),
        ("mindegy", "doesn't matter"), ("na", "well / come on"), ("hoppá", "oops!"),
    ],
    "Shops & services": [
        ("pénztár", "checkout"), ("kassza", "cash register"), ("eladó", "sales assistant"),
        ("vásárló", "customer"), ("ár", "price"), ("akció", "sale / special offer"),
        ("kedvezmény", "discount"), ("leárazás", "sale / markdown"), ("címke", "label"),
        ("blokk", "receipt"), ("nyugta", "receipt"), ("garancia", "warranty"),
        ("csere", "exchange"), ("méret", "size"), ("próbafülke", "fitting room"),
        ("bevásárlókosár", "shopping basket"), ("bevásárlókocsi", "shopping cart"),
        ("polc", "shelf"), ("pult", "counter"), ("sor", "queue"), ("pékség", "bakery"),
        ("piac", "market"), ("drogéria", "drugstore"), ("hentes", "butcher"),
    ],
    "Food & drink": [
        ("sajt", "cheese"), ("tojás", "egg"), ("vaj", "butter"), ("méz", "honey"),
        ("lekvár", "jam"), ("rizs", "rice"), ("tészta", "pasta"), ("burgonya", "potato"),
        ("krumpli", "potato (everyday)"), ("hagyma", "onion"), ("fokhagyma", "garlic"),
        ("paradicsom", "tomato"), ("uborka", "cucumber"), ("répa", "carrot"),
        ("narancs", "orange"), ("körte", "pear"), ("szőlő", "grapes"), ("eper", "strawberry"),
        ("cseresznye", "cherry"), ("citrom", "lemon"), ("cukor", "sugar"), ("só", "salt"),
        ("bors", "pepper"), ("leves", "soup"), ("saláta", "salad"), ("csirke", "chicken"),
    ],
    "Clothing": [
        ("ruha", "dress / clothing"), ("póló", "T-shirt"), ("ing", "shirt"), ("nadrág", "trousers"),
        ("szoknya", "skirt"), ("cipő", "shoe"), ("zokni", "sock"), ("kabát", "coat"),
        ("dzseki", "jacket"), ("pulóver", "sweater"), ("sapka", "hat"), ("sál", "scarf"),
        ("kesztyű", "glove"), ("csizma", "boot"), ("farmer", "jeans"), ("öv", "belt"),
        ("zseb", "pocket"), ("gomb", "button"),
    ],
    "Home & everyday objects": [
        ("nappali", "living room"), ("hálószoba", "bedroom"), ("fürdőszoba", "bathroom"),
        ("konyha", "kitchen"), ("erkély", "balcony"), ("lépcső", "stairs"), ("emelet", "floor / storey"),
        ("fal", "wall"), ("padló", "floor"), ("mennyezet", "ceiling"), ("asztal", "table"),
        ("szék", "chair"), ("kanapé", "sofa"), ("ágy", "bed"), ("párna", "pillow"),
        ("takaró", "blanket"), ("lámpa", "lamp"), ("szőnyeg", "rug"), ("függöny", "curtain"),
        ("hűtő", "fridge"), ("sütő", "oven"), ("mosógép", "washing machine"),
        ("mosogató", "kitchen sink"), ("tükör", "mirror"),
    ],
    "City & directions": [
        ("híd", "bridge"), ("tér", "square"), ("park", "park"), ("járda", "sidewalk"),
        ("zebra", "crosswalk"), ("kereszteződés", "intersection"), ("közlekedési lámpa", "traffic light"),
        ("sarok", "corner"), ("út", "road"), ("könyvtár", "library"), ("múzeum", "museum"),
        ("mozi", "cinema"), ("színház", "theater"), ("templom", "church"),
        ("rendőrség", "police station"), ("bejárat", "entrance"), ("kijárat", "exit"),
        ("lift", "elevator"), ("szökőkút", "fountain"), ("aluljáró", "underpass"),
        ("postahivatal", "post office"), ("bank", "bank"),
    ],
    "Travel & transport": [
        ("repülőgép", "airplane"), ("repülőtér", "airport"), ("villamos", "tram"),
        ("trolibusz", "trolleybus"), ("taxi", "taxi"), ("hajó", "ship"), ("kerékpár", "bicycle"),
        ("bérlet", "travel pass"), ("menetrend", "timetable"), ("peron", "platform"),
        ("indulás", "departure"), ("érkezés", "arrival"), ("késés", "delay"),
        ("átszállás", "transfer"), ("útlevél", "passport"), ("bőrönd", "suitcase"),
        ("hátizsák", "backpack"), ("térkép", "map"), ("foglalás", "reservation"),
        ("szállás", "accommodation"), ("utas", "passenger"), ("sofőr", "driver"),
        ("jegyautomata", "ticket machine"), ("retúrjegy", "return ticket"),
    ],
    "Work & study": [
        ("iroda", "office"), ("munkahely", "workplace"), ("főnök", "boss"), ("kolléga", "colleague"),
        ("csapat", "team"), ("megbeszélés", "meeting"), ("feladat", "task"), ("szünet", "break"),
        ("projekt", "project"), ("határidő", "deadline"), ("fizetés", "salary"), ("állás", "job / position"),
        ("önéletrajz", "CV / résumé"), ("jelentkezés", "application"), ("interjú", "interview"),
        ("ügyfél", "client"), ("nyomtató", "printer"), ("papír", "paper"), ("toll", "pen"),
        ("füzet", "notebook"), ("tanár", "teacher"), ("diák", "student"),
    ],
    "Health & body": [
        ("fej", "head"), ("arc", "face"), ("szem", "eye"), ("fül", "ear"), ("orr", "nose"),
        ("száj", "mouth"), ("fog", "tooth"), ("nyak", "neck"), ("váll", "shoulder"), ("kar", "arm"),
        ("kéz", "hand"), ("ujj", "finger"), ("has", "stomach"), ("hát", "back"), ("láb", "leg / foot"),
        ("szív", "heart"), ("láz", "fever"), ("köhögés", "cough"), ("fájdalom", "pain"),
        ("gyógyszer", "medicine"),
    ],
    "People & relationships": [
        ("anya", "mother"), ("apa", "father"), ("anyuka", "mom (affectionate)"), ("apuka", "dad (affectionate)"),
        ("báty", "older brother"), ("öcs", "younger brother"), ("nővér", "older sister / nurse"),
        ("húg", "younger sister"), ("szülő", "parent"), ("gyerek", "child"), ("iker", "twin"),
        ("unokahúg", "niece"), ("unokaöcs", "nephew"), ("nagynéni", "aunt"), ("nagybácsi", "uncle"),
        ("unoka", "grandchild"), ("pár", "partner / couple"), ("szomszéd", "neighbor"),
    ],
    "Time & weather": [
        ("tavasz", "spring"), ("nyár", "summer"), ("ősz", "autumn"), ("tél", "winter"),
        ("reggel", "morning"), ("délután", "afternoon"), ("este", "evening"), ("éjszaka", "night"),
        ("perc", "minute"), ("óra", "hour / clock"), ("hét", "week"), ("hónap", "month"),
        ("év", "year"), ("hétvége", "weekend"), ("tegnap", "yesterday"), ("tegnapelőtt", "day before yesterday"),
        ("korán", "early"), ("későn", "late"), ("eső", "rain"), ("hó", "snow"),
    ],
    "Nature & animals": [
        ("kutya", "dog"), ("macska", "cat"), ("ló", "horse"), ("tehén", "cow"), ("juh", "sheep"),
        ("kecske", "goat"), ("disznó", "pig"), ("tyúk", "hen"), ("kacsa", "duck"), ("nyúl", "rabbit"),
        ("egér", "mouse"), ("medve", "bear"), ("róka", "fox"), ("farkas", "wolf"), ("szarvas", "deer"),
        ("méh", "bee"), ("pillangó", "butterfly"), ("fa", "tree"), ("virág", "flower"), ("erdő", "forest"),
        ("tó", "lake"), ("folyó", "river"), ("hegy", "mountain"), ("tenger", "sea"),
        ("felhő", "cloud"), ("csillag", "star"),
    ],
    "Technology": [
        ("számítógép", "computer"), ("laptop", "laptop"), ("mobiltelefon", "mobile phone"),
        ("képernyő", "screen"), ("billentyűzet", "keyboard"), ("egér", "computer mouse"),
        ("töltő", "charger"), ("akkumulátor", "battery"), ("elem", "battery cell"), ("kábel", "cable"),
        ("internet", "internet"), ("weboldal", "website"), ("jelszó", "password"), ("fájl", "file"),
        ("mappa", "folder"), ("alkalmazás", "app"), ("üzenet", "message"), ("fénykép", "photograph"),
        ("videó", "video"), ("kamera", "camera"),
    ],
    "Leisure & hobbies": [
        ("zene", "music"), ("dal", "song"), ("film", "movie"), ("könyv", "book"), ("játék", "game"),
        ("sport", "sport"), ("foci", "football"), ("úszás", "swimming"), ("futás", "running"),
        ("séta", "walk"), ("tánc", "dance"), ("főzés", "cooking"), ("festés", "painting"),
        ("hobbi", "hobby"), ("vakáció", "vacation"), ("kirándulás", "hike / day trip"),
        ("koncert", "concert"), ("szabadidő", "free time"), ("fotózás", "photography"),
        ("társasjáték", "board game"),
    ],
    "Common verbs": [
        ("vár", "waits"), ("alszik", "sleeps"), ("fut", "runs"), ("sétál", "walks"),
        ("hallgat", "listens"), ("néz", "watches"), ("olvas", "reads"), ("ír", "writes"),
        ("tanul", "studies"), ("dolgozik", "works"), ("főz", "cooks"), ("mos", "washes"),
        ("takarít", "cleans"), ("vásárol", "shops"), ("fizet", "pays"), ("kér", "asks"),
        ("ad", "gives"), ("kap", "receives"), ("segít", "helps"), ("keres", "looks for"),
        ("talál", "finds"), ("hív", "calls"), ("nyit", "opens"), ("zár", "closes"),
        ("kezd", "starts"), ("befejez", "finishes"), ("marad", "stays"), ("utazik", "travels"),
        ("játszik", "plays"), ("énekel", "sings"),
    ],
    "Descriptions": [
        ("csendes", "quiet"), ("hangos", "loud"), ("tiszta", "clean"), ("piszkos", "dirty"),
        ("puha", "soft"), ("kemény", "hard"), ("könnyű", "light / easy"), ("nehéz", "heavy / difficult"),
        ("hosszú", "long"), ("rövid", "short"), ("magas", "tall / high"), ("alacsony", "short / low"),
        ("széles", "wide"), ("keskeny", "narrow"), ("egyszerű", "simple"), ("bonyolult", "complicated"),
        ("érdekes", "interesting"), ("unalmas", "boring"), ("világos", "bright"), ("sötét", "dark"),
        ("barátságos", "friendly"), ("veszélyes", "dangerous"), ("egészséges", "healthy"), ("beteg", "ill"),
    ],
    "Numbers & money": [
        ("négy", "four"), ("öt", "five"), ("hat", "six"), ("hét", "seven"), ("nyolc", "eight"),
        ("kilenc", "nine"), ("tíz", "ten"), ("tizenegy", "eleven"), ("tizenkettő", "twelve"),
        ("tizenhárom", "thirteen"), ("tizennégy", "fourteen"), ("tizenöt", "fifteen"),
        ("tizenhat", "sixteen"), ("tizenhét", "seventeen"), ("tizennyolc", "eighteen"),
        ("tizenkilenc", "nineteen"), ("húsz", "twenty"), ("ezer", "one thousand"),
    ],
    "Emergencies": [
        ("segítség", "help"), ("rendőr", "police officer"), ("tűz", "fire"), ("baleset", "accident"),
        ("veszély", "danger"), ("mentőautó", "ambulance"), ("tűzoltó", "firefighter"),
        ("sérülés", "injury"), ("elsősegély", "first aid"), ("vészhelyzet", "emergency"),
        ("eltűnt", "missing / disappeared"), ("lopás", "theft"), ("betörő", "burglar"),
        ("biztonság", "safety"),
    ],
}


EXAMPLES: dict[str, tuple[str, str]] = {
    "mi újság": ("Na, mi újság veled?", "So, what's new with you?"),
    "mizu": ("Mizu? Rég láttalak.", "What's up? I haven't seen you in a while."),
    "köszi": ("Köszi a segítséget!", "Thanks for the help!"),
    "bocsi": ("Bocsi, elkéstem.", "Sorry, I'm late."),
    "csá": ("Csá, holnap találkozunk!", "Bye, see you tomorrow!"),
    "hellóka": ("Hellóka, minden rendben?", "Hey there, is everything okay?"),
    "tök jó": ("Ez a hely tök jó.", "This place is really cool."),
    "menő": ("Ez a kabát nagyon menő.", "This jacket is really cool."),
    "király": ("Király, jössz te is!", "Awesome, you're coming too!"),
    "szuper": ("A terved szuper.", "Your plan is great."),
    "gáz": ("Ez most elég gáz.", "This is pretty awkward."),
    "para": ("Ez a hang egy kicsit para.", "That sound is a little creepy."),
    "haver": ("Haver, várj meg!", "Buddy, wait for me!"),
    "csaj": ("Az a csaj nagyon kedves.", "That girl is very kind."),
    "pasi": ("Az a pasi a barátom.", "That guy is my friend."),
    "buli": ("Szombaton buli lesz.", "There's a party on Saturday."),
    "pia": ("A buliban van pia.", "There's booze at the party."),
    "kaja": ("A kaja nagyon finom.", "The food is really good."),
    "laza": ("Ma laza napom van.", "I'm having a relaxed day today."),
    "tuti": ("Tuti, hogy jövök.", "I'm definitely coming."),
    "simán": ("Simán segítek neked.", "I'll gladly help you."),
    "mindegy": ("Nekem mindegy, hová megyünk.", "I don't mind where we go."),
    "na": ("Na, induljunk!", "Come on, let's go!"),
    "hoppá": ("Hoppá, leesett a kulcs.", "Oops, the key fell."),
    "repülőgép": ("A repülőgép délben indul.", "The airplane leaves at noon."),
    "repülőtér": ("A repülőtér közel van.", "The airport is nearby."),
    "villamos": ("A villamos késik.", "The tram is late."),
    "trolibusz": ("A trolibusz megáll a téren.", "The trolleybus stops in the square."),
    "taxi": ("A taxi az ajtó előtt vár.", "The taxi is waiting outside the door."),
    "hajó": ("A hajó reggel indul.", "The ship leaves in the morning."),
    "kerékpár": ("A kerékpár a ház előtt van.", "The bicycle is in front of the house."),
    "bérlet": ("A bérlet egész hónapra érvényes.", "The travel pass is valid for the whole month."),
    "menetrend": ("Megnézem a menetrendet.", "I'll check the timetable."),
    "peron": ("A vonat a második peronról indul.", "The train leaves from platform two."),
    "indulás": ("Az indulás reggel hatkor lesz.", "Departure is at six in the morning."),
    "érkezés": ("Az érkezés este várható.", "Arrival is expected in the evening."),
    "késés": ("A késés miatt lekéstük a csatlakozást.", "We missed the connection because of the delay."),
    "átszállás": ("Az átszállás a következő állomáson van.", "The transfer is at the next station."),
    "útlevél": ("Az útlevelet magammal viszem.", "I'll take my passport with me."),
    "bőrönd": ("A bőrönd nehéz.", "The suitcase is heavy."),
    "hátizsák": ("A hátizsák a széken van.", "The backpack is on the chair."),
    "térkép": ("A térképen látom az utat.", "I can see the road on the map."),
    "foglalás": ("A foglalás a nevemre szól.", "The reservation is under my name."),
    "szállás": ("A szállás a tó mellett van.", "The accommodation is beside the lake."),
    "utas": ("Az utas felszáll a buszra.", "The passenger gets on the bus."),
    "sofőr": ("A sofőr gyorsan vezet.", "The driver is driving fast."),
    "jegyautomata": ("A jegyautomata a megállóban van.", "The ticket machine is at the stop."),
    "retúrjegy": ("Retúrjegyet kérek Budapestre.", "I'd like a return ticket to Budapest."),
    "pénztár": ("A pénztár a bolt végén van.", "The checkout is at the back of the shop."),
    "kassza": ("A kassza most nyitva van.", "The till is open now."),
    "eladó": ("Az eladó segít nekem.", "The sales assistant is helping me."),
    "vásárló": ("A vásárló már fizet.", "The customer is paying already."),
    "ár": ("Az ár túl magas.", "The price is too high."),
    "akció": ("Ma akció van a boltban.", "There's a sale in the shop today."),
    "kedvezmény": ("Diákigazolvánnyal kedvezmény jár.", "Students get a discount with a student ID."),
    "leárazás": ("A leárazás hétfőn kezdődik.", "The sale starts on Monday."),
    "címke": ("A címkén rajta van az ár.", "The price is on the label."),
    "blokk": ("Kérem a blokkot.", "May I have the receipt?"),
    "nyugta": ("A nyugtát elteszem.", "I'll keep the receipt."),
    "garancia": ("Erre a telefonra két év garancia van.", "This phone has a two-year warranty."),
    "csere": ("A csere ingyenes.", "The exchange is free."),
    "méret": ("Milyen méretet keres?", "What size are you looking for?"),
    "próbafülke": ("A próbafülke a sarokban van.", "The fitting room is in the corner."),
    "bevásárlókosár": ("A bevásárlókosár az ajtó mellett van.", "The shopping basket is beside the door."),
    "bevásárlókocsi": ("A bevásárlókocsi üres.", "The shopping cart is empty."),
    "polc": ("A polcon van a tej.", "The milk is on the shelf."),
    "pult": ("A pultnál lehet fizetni.", "You can pay at the counter."),
    "sor": ("Hosszú sor áll a pénztárnál.", "There's a long queue at the checkout."),
    "pékség": ("A pékség reggel hétkor nyit.", "The bakery opens at seven in the morning."),
    "piac": ("A piac szombaton is nyitva van.", "The market is open on Saturdays too."),
    "drogéria": ("A drogéria a gyógyszertár mellett van.", "The drugstore is next to the pharmacy."),
    "hentes": ("A hentes friss húst árul.", "The butcher sells fresh meat."),
    "só": ("Kevés só van az ételben.", "There's little salt in the food."),
    "bors": ("Egy kis borsot teszek a levesbe.", "I'll add a little pepper to the soup."),
    "fej": ("Fáj a fejem.", "My head hurts."), "arc": ("Megmosom az arcom.", "I'll wash my face."),
    "szem": ("A szemem nagyon fáradt.", "My eye is very tired."), "fül": ("Fáj a fülem.", "My ear hurts."),
    "orr": ("Az orrom piros.", "My nose is red."), "száj": ("Tátva van a szája.", "His mouth is open."),
    "fog": ("Fáj a fogam.", "My tooth hurts."), "nyak": ("Fáj a nyakam.", "My neck hurts."),
    "váll": ("Fáj a vállam.", "My shoulder hurts."), "kar": ("Fáj a karom.", "My arm hurts."),
    "kéz": ("Fáj a kezem.", "My hand hurts."), "ujj": ("Fáj az ujjam.", "My finger hurts."),
    "has": ("Fáj a hasam.", "My stomach hurts."), "hát": ("Fáj a hátam.", "My back hurts."),
    "láb": ("Fáj a lábam.", "My leg hurts."), "szív": ("A szívem gyorsan ver.", "My heart is beating fast."),
    "láz": ("Magas lázam van.", "I have a high fever."),
    "köhögés": ("A köhögés zavar éjszaka.", "The cough bothers me at night."),
    "fájdalom": ("Erős fájdalmat érzek.", "I feel severe pain."),
    "gyógyszer": ("Beveszem a gyógyszert.", "I'll take the medicine."),
    "farmer": ("Ez a farmer kényelmes.", "These jeans are comfortable."),
    "zene": ("A zene megnyugtat.", "Music helps me relax."),
    "dal": ("Ez a dal nagyon szép.", "This song is very beautiful."),
    "film": ("A film érdekes.", "The movie is interesting."),
    "könyv": ("A könyv izgalmas.", "The book is exciting."),
    "játék": ("A játék nagyon szórakoztató.", "The game is very entertaining."),
    "sport": ("A sport egészséges.", "Sport is healthy."),
    "foci": ("A foci a kedvenc sportom.", "Football is my favorite sport."),
    "úszás": ("Az úszás jó mozgás.", "Swimming is good exercise."),
    "futás": ("A futás jó reggel.", "Running is good in the morning."),
    "séta": ("A séta segít pihenni.", "A walk helps me relax."),
    "tánc": ("A tánc jó mozgás.", "Dancing is good exercise."),
    "főzés": ("A főzés kikapcsol.", "Cooking helps me unwind."),
    "festés": ("A festés megnyugtat.", "Painting helps me relax."),
    "hobbi": ("A fotózás a hobbim.", "Photography is my hobby."),
    "vakáció": ("A vakáció jövő héten kezdődik.", "Vacation starts next week."),
    "kirándulás": ("A kirándulás holnap lesz.", "The day trip is tomorrow."),
    "koncert": ("A koncert este kezdődik.", "The concert starts this evening."),
    "szabadidő": ("A szabadidő nagyon fontos.", "Free time is very important."),
    "fotózás": ("A fotózás a hobbim.", "Photography is my hobby."),
    "társasjáték": ("A társasjátékot együtt játsszuk.", "We play the board game together."),
    "tavasz": ("Tavasszal virágoznak a fák.", "The trees blossom in spring."),
    "nyár": ("Nyáron sokat süt a nap.", "The sun shines a lot in summer."),
    "ősz": ("Ősszel lehullanak a levelek.", "The leaves fall in autumn."),
    "tél": ("Télen gyakran havazik.", "It often snows in winter."),
    "reggel": ("Reggel kávét iszom.", "I drink coffee in the morning."),
    "délután": ("Délután találkozunk.", "We'll meet in the afternoon."),
    "este": ("Este otthon vagyok.", "I'm at home in the evening."),
    "éjszaka": ("Éjszaka csend van.", "It's quiet at night."),
    "perc": ("Várj egy percet!", "Wait a minute!"),
    "óra": ("Az óra háromkor kezdődik.", "The lesson starts at three."),
    "hét": ("A szám hét.", "The number is seven."),
    "hónap": ("A hónap végén utazom.", "I'm traveling at the end of the month."),
    "év": ("Egy év múlva hazautazom.", "I'll travel home in a year."),
    "hétvége": ("Hétvégén pihenek.", "I rest at the weekend."),
    "tegnap": ("Tegnap esett az eső.", "It rained yesterday."),
    "tegnapelőtt": ("Tegnapelőtt moziba mentünk.", "We went to the cinema the day before yesterday."),
    "korán": ("Ma korán kelek.", "I'm getting up early today."),
    "későn": ("Tegnap későn feküdtem le.", "I went to bed late yesterday."),
    "eső": ("Eső esik.", "It's raining."), "hó": ("Télen hó esik.", "It snows in winter."),
    "négy": ("A szám négy.", "The number is four."), "öt": ("A szám öt.", "The number is five."),
    "hat": ("A szám hat.", "The number is six."),
    "nyolc": ("A szám nyolc.", "The number is eight."), "kilenc": ("A szám kilenc.", "The number is nine."),
    "tíz": ("A szám tíz.", "The number is ten."), "tizenegy": ("A szám tizenegy.", "The number is eleven."),
    "tizenkettő": ("A szám tizenkettő.", "The number is twelve."),
    "tizenhárom": ("A szám tizenhárom.", "The number is thirteen."),
    "tizennégy": ("A szám tizennégy.", "The number is fourteen."),
    "tizenöt": ("A szám tizenöt.", "The number is fifteen."),
    "tizenhat": ("A szám tizenhat.", "The number is sixteen."),
    "tizenhét": ("A szám tizenhét.", "The number is seventeen."),
    "tizennyolc": ("A szám tizennyolc.", "The number is eighteen."),
    "tizenkilenc": ("A szám tizenkilenc.", "The number is nineteen."),
    "húsz": ("A szám húsz.", "The number is twenty."),
    "ezer": ("Ezer forintba kerül.", "It costs one thousand forints."),
    "segítség": ("Kérek segítséget.", "I need help."), "rendőr": ("Hívja a rendőrt!", "Call the police officer!"),
    "tűz": ("Tűz van!", "There is a fire!"), "baleset": ("A baleset az úton történt.", "The accident happened on the road."),
    "veszély": ("Ez nagy veszély.", "This is a great danger."),
    "mentőautó": ("Hívjuk a mentőautót!", "Let's call the ambulance!"),
    "tűzoltó": ("A tűzoltó már úton van.", "The firefighter is already on the way."),
    "sérülés": ("A sérülés nem súlyos.", "The injury isn't serious."),
    "elsősegély": ("Elsősegélyre van szükségünk.", "We need first aid."),
    "vészhelyzet": ("Vészhelyzet esetén hívjon segítséget.", "Call for help in an emergency."),
    "eltűnt": ("A gyerek eltűnt.", "The child is missing."), "lopás": ("A lopás tegnap történt.", "The theft happened yesterday."),
    "betörő": ("A betörő elmenekült.", "The burglar ran away."),
    "biztonság": ("A biztonság nagyon fontos.", "Safety is very important."),
    "vár": ("A buszmegállóban várok.", "I wait at the bus stop."),
    "alszik": ("A gyerek alszik.", "The child is sleeping."), "fut": ("A fiú gyorsan fut.", "The boy runs quickly."),
    "sétál": ("A barátom a parkban sétál.", "My friend is walking in the park."),
    "hallgat": ("A lány zenét hallgat.", "The girl is listening to music."),
    "néz": ("A család filmet néz.", "The family is watching a movie."),
    "olvas": ("A tanár egy könyvet olvas.", "The teacher is reading a book."),
    "ír": ("A diák levelet ír.", "The student is writing a letter."),
    "tanul": ("A barátom magyarul tanul.", "My friend is studying Hungarian."),
    "dolgozik": ("Az anya otthonról dolgozik.", "The mother works from home."),
    "főz": ("Az apa vacsorát főz.", "The father is cooking dinner."),
    "mos": ("A fiú kezet mos.", "The boy is washing his hands."),
    "takarít": ("A család szombaton takarít.", "The family cleans on Saturday."),
    "vásárol": ("A vásárló kenyeret vásárol.", "The customer is buying bread."),
    "fizet": ("A férfi készpénzzel fizet.", "The man is paying in cash."),
    "kér": ("A vendég egy pohár vizet kér.", "The guest asks for a glass of water."),
    "ad": ("A tanár feladatot ad.", "The teacher gives an assignment."),
    "kap": ("A lány üzenetet kap.", "The girl receives a message."),
    "segít": ("A szomszéd segít nekünk.", "The neighbor helps us."),
    "keres": ("A fiú egy kulcsot keres.", "The boy is looking for a key."),
    "talál": ("A nő egy jó boltot talál.", "The woman finds a good shop."),
    "hív": ("A fiú felhívja az anyukáját.", "The boy calls his mom."),
    "nyit": ("A bolt reggel nyit.", "The shop opens in the morning."),
    "zár": ("A könyvtár este zár.", "The library closes in the evening."),
    "kezd": ("A megbeszélés kilenckor kezdődik.", "The meeting starts at nine."),
    "befejez": ("A diák befejezi a feladatot.", "The student finishes the task."),
    "marad": ("A barátom ma otthon marad.", "My friend is staying home today."),
    "utazik": ("A család vonattal utazik.", "The family is traveling by train."),
    "játszik": ("A gyerek a kertben játszik.", "The child is playing in the garden."),
    "énekel": ("A lány egy dalt énekel.", "The girl is singing a song."),
    "csendes": ("A szoba csendes.", "The room is quiet."), "hangos": ("A zene hangos.", "The music is loud."),
    "tiszta": ("A víz tiszta.", "The water is clean."), "piszkos": ("A cipő piszkos.", "The shoe is dirty."),
    "puha": ("A párna puha.", "The pillow is soft."), "kemény": ("A kenyér kemény.", "The bread is hard."),
    "könnyű": ("A táska könnyű.", "The bag is light."), "nehéz": ("A feladat nehéz.", "The task is difficult."),
    "hosszú": ("A film hosszú.", "The movie is long."), "rövid": ("A szünet rövid.", "The break is short."),
    "magas": ("A torony magas.", "The tower is tall."), "alacsony": ("A szék alacsony.", "The chair is low."),
    "széles": ("Az út széles.", "The road is wide."), "keskeny": ("Az utca keskeny.", "The street is narrow."),
    "egyszerű": ("A kérdés egyszerű.", "The question is simple."),
    "bonyolult": ("A szabály bonyolult.", "The rule is complicated."),
    "érdekes": ("A könyv érdekes.", "The book is interesting."),
    "unalmas": ("A film unalmas.", "The movie is boring."), "világos": ("A szoba világos.", "The room is bright."),
    "sötét": ("Az ég sötét.", "The sky is dark."),
    "barátságos": ("A szomszéd barátságos.", "The neighbor is friendly."),
    "veszélyes": ("A tűz veszélyes.", "Fire is dangerous."),
    "egészséges": ("A reggeli egészséges.", "Breakfast is healthy."), "beteg": ("A barátom beteg.", "My friend is ill."),
}

TOPIC_EXAMPLES: dict[tuple[str, str], tuple[str, str]] = {
    ("Time & weather", "hét"): ("Egy hét múlva indulunk.", "We're leaving in a week."),
}


def ascii_slug(value: str) -> str:
    value = unicodedata.normalize("NFKD", value.casefold())
    value = "".join(char for char in value if not unicodedata.combining(char))
    return "-".join(re.findall(r"[a-z0-9]+", value))


def article(word: str) -> str:
    first = word.casefold().lstrip("(")[0]
    return "az" if first in "aáeéiíoóöőuúüű" else "a"


def noun_example(topic: str, word: str, english: str) -> tuple[str, str]:
    art = article(word)
    english = re.split(r"\s*/\s*|\s*\(", english, maxsplit=1)[0].strip()
    if topic == "Food & drink":
        return f"{art.capitalize()} {word} finom.", f"The {english} is tasty."
    if topic == "Clothing":
        return f"Ez {art} {word} új.", f"This {english} is new."
    if topic == "Home & everyday objects":
        return f"{art.capitalize()} {word} a lakásban van.", f"The {english} is in the apartment."
    if topic == "City & directions":
        return f"{art.capitalize()} {word} a városban van.", f"The {english} is in the city."
    if topic == "Travel & transport":
        return f"{art.capitalize()} {word} kell az utazáshoz.", f"The {english} is needed for travel."
    if topic == "Work & study":
        return f"{art.capitalize()} {word} fontos a munkában.", f"The {english} is important at work."
    if topic == "People & relationships":
        return f"{art.capitalize()} {word} már itt van.", f"The {english} is already here."
    if topic == "Nature & animals":
        return f"{art.capitalize()} {word} nagyon szép.", f"The {english} is very beautiful."
    if topic == "Technology":
        return f"{art.capitalize()} {word} jól működik.", f"The {english} works well."
    if topic == "Leisure & hobbies":
        return f"{art.capitalize()} {word} szórakoztató.", f"The {english} is fun."
    raise ValueError(f"No example rule for {topic}")


def build_cards() -> list[dict[str, str]]:
    cards: list[dict[str, str]] = []
    seen_words: set[tuple[str, str]] = set()
    for topic, entries in WORDS.items():
        for word, english in entries:
            topic_word = (topic.casefold(), word.casefold())
            if topic_word in seen_words:
                raise ValueError(f"Duplicate extended headword: {word}")
            seen_words.add(topic_word)
            example = TOPIC_EXAMPLES.get((topic, word), EXAMPLES.get(word))
            if example is None:
                if topic == "Street talk":
                    raise ValueError(f"Missing conversation example for {word}")
                if topic == "Common verbs":
                    raise ValueError(f"Missing verb example for {word}")
                if topic == "Descriptions":
                    raise ValueError(f"Missing adjective example for {word}")
                if topic == "Numbers & money":
                    raise ValueError(f"Missing number example for {word}")
                if topic == "Emergencies":
                    raise ValueError(f"Missing emergency example for {word}")
                if topic == "Health & body":
                    raise ValueError(f"Missing health example for {word}")
                if topic == "Time & weather":
                    raise ValueError(f"Missing time/weather example for {word}")
                example = noun_example(topic, word, english)

            card_id = f"x-{ascii_slug(topic)}-{ascii_slug(word)}"
            cards.append({
                "id": card_id,
                "hungarian": word,
                "english": english,
                "kind": "word",
                "topic": topic,
                "exampleHungarian": example[0],
                "exampleEnglish": example[1],
                "exampleAudioId": f"ex-{card_id}",
            })

    if len(cards) != 394:
        raise ValueError(f"Expected 394 new words, got {len(cards)}")
    return cards


def main() -> None:
    cards = build_cards()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps({"version": 1, "cards": cards}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {len(cards)} paired word cards to {OUTPUT}")
    for topic, words in WORDS.items():
        print(f"  {topic}: {len(words)}")


if __name__ == "__main__":
    main()
