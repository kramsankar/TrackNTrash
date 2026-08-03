/**
 * Console localisation. Tamil and Hindi to start.
 *
 * Two sources, deliberately kept apart:
 *
 *  - UI chrome (menu, buttons, headings) is bundled here. It must render before any
 *    request completes, and a network hiccup must never leave someone staring at a blank
 *    navigation bar.
 *  - Data (store names, checkpoints, shipment states, exception types) comes from the API,
 *    because it is data — see ops.Translation. Fetched once per language and cached.
 *
 * Missing strings fall back to English rather than showing a key or a gap. A half-translated
 * screen is usable; a screen full of `nav.orders` is not.
 */

export type Lang = "en" | "ta" | "hi";

export const LANGS: { code: Lang; native: string; english: string }[] = [
  { code: "en", native: "English", english: "English" },
  { code: "ta", native: "தமிழ்", english: "Tamil" },
  { code: "hi", native: "हिन्दी", english: "Hindi" },
];

const STORAGE_KEY = "tnt.lang";

type Bundle = Record<string, string>;

const en: Bundle = {
  "app.name": "TrackNTrash",
  "app.tagline": "Dispatch Track & Trace",
  "nav.menu": "Menu",
  "nav.dashboard": "Dashboard",
  "nav.orders": "Orders",
  "nav.trips": "Trips & Loading",
  "nav.manifests": "Manifests (ASN)",
  "nav.assets": "Asset Master",
  "nav.lookup": "Line Lookup",
  "nav.items": "Item Counting",
  "nav.cameras": "Cameras & Map",
  "nav.exceptions": "Exceptions",
  "nav.products": "Products",
  "nav.stores": "Stores",
  "nav.zones": "Zones",
  "nav.racks": "Racks",
  "nav.vehicles": "Vehicles",
  "nav.devices": "Devices",
  "nav.roles": "Roles",
  "nav.users": "Users",
  "nav.mapping": "Role Mapping",
  "group.Overview": "Overview",
  "group.Operations": "Operations",
  "group.Inspection": "Inspection",
  "group.Monitoring": "Monitoring",
  "group.Masters": "Masters",
  "group.Administration": "Administration",
  "state.live": "live",
  "state.offline": "offline",
  "action.signOut": "Sign out",
  "action.signIn": "Sign in",
  "label.noRole": "no role",
  "label.language": "Language",
  "label.username": "Username",
  "label.password": "Password",
  "login.title": "Sign in",
  "login.wrong": "Wrong username or password.",
};

const ta: Bundle = {
  "app.tagline": "அனுப்புதல் கண்காணிப்பு",
  "nav.menu": "பட்டி",
  "nav.dashboard": "முகப்பு",
  "nav.orders": "ஆர்டர்கள்",
  "nav.trips": "பயணங்கள் மற்றும் ஏற்றுதல்",
  "nav.manifests": "பட்டியல் (ASN)",
  "nav.assets": "சொத்து பதிவேடு",
  "nav.lookup": "வரி தேடல்",
  "nav.items": "பொருள் எண்ணிக்கை",
  "nav.cameras": "கேமராக்கள் மற்றும் வரைபடம்",
  "nav.exceptions": "விதிவிலக்குகள்",
  "nav.products": "பொருட்கள்",
  "nav.stores": "கடைகள்",
  "nav.zones": "மண்டலங்கள்",
  "nav.racks": "அடுக்குகள்",
  "nav.vehicles": "வாகனங்கள்",
  "nav.devices": "சாதனங்கள்",
  "nav.roles": "பணிகள்",
  "nav.users": "பயனர்கள்",
  "nav.mapping": "பணி அனுமதிகள்",
  "group.Overview": "மேலோட்டம்",
  "group.Operations": "செயல்பாடுகள்",
  "group.Inspection": "சோதனை",
  "group.Monitoring": "கண்காணிப்பு",
  "group.Masters": "முதன்மை தரவு",
  "group.Administration": "நிர்வாகம்",
  "state.live": "நேரலை",
  "state.offline": "இணைப்பு இல்லை",
  "action.signOut": "வெளியேறு",
  "action.signIn": "உள்நுழை",
  "label.noRole": "பணி இல்லை",
  "label.language": "மொழி",
  "label.username": "பயனர் பெயர்",
  "label.password": "கடவுச்சொல்",
  "login.title": "உள்நுழைவு",
  "login.wrong": "பயனர் பெயர் அல்லது கடவுச்சொல் தவறு.",
};

const hi: Bundle = {
  "app.tagline": "प्रेषण ट्रैकिंग",
  "nav.menu": "मेन्यू",
  "nav.dashboard": "डैशबोर्ड",
  "nav.orders": "ऑर्डर",
  "nav.trips": "ट्रिप और लोडिंग",
  "nav.manifests": "सूची (ASN)",
  "nav.assets": "संपत्ति मास्टर",
  "nav.lookup": "लाइन खोज",
  "nav.items": "वस्तु गणना",
  "nav.cameras": "कैमरे और मानचित्र",
  "nav.exceptions": "अपवाद",
  "nav.products": "उत्पाद",
  "nav.stores": "स्टोर",
  "nav.zones": "क्षेत्र",
  "nav.racks": "रैक",
  "nav.vehicles": "वाहन",
  "nav.devices": "उपकरण",
  "nav.roles": "भूमिकाएँ",
  "nav.users": "उपयोगकर्ता",
  "nav.mapping": "भूमिका अनुमतियाँ",
  "group.Overview": "अवलोकन",
  "group.Operations": "संचालन",
  "group.Inspection": "निरीक्षण",
  "group.Monitoring": "निगरानी",
  "group.Masters": "मास्टर डेटा",
  "group.Administration": "प्रशासन",
  "state.live": "लाइव",
  "state.offline": "ऑफ़लाइन",
  "action.signOut": "साइन आउट",
  "action.signIn": "साइन इन",
  "label.noRole": "कोई भूमिका नहीं",
  "label.language": "भाषा",
  "label.username": "उपयोगकर्ता नाम",
  "label.password": "पासवर्ड",
  "login.title": "साइन इन",
  "login.wrong": "उपयोगकर्ता नाम या पासवर्ड गलत है।",
};

const BUNDLES: Record<Lang, Bundle> = { en, ta, hi };

export function getLang(): Lang {
  const stored = localStorage.getItem(STORAGE_KEY) as Lang | null;
  if (stored && BUNDLES[stored]) return stored;
  // Fall back to what the browser asks for before defaulting to English, so a Tamil-locale
  // machine opens in Tamil without anyone changing a setting.
  const browser = (navigator.language || "en").slice(0, 2) as Lang;
  return BUNDLES[browser] ? browser : "en";
}

export function setLang(lang: Lang) {
  localStorage.setItem(STORAGE_KEY, lang);
  refCache = {};                                  // reference bundle is per-language
}

/** Translate a UI key. Unknown keys fall back to English, then to the key itself. */
export function t(key: string, lang: Lang = getLang()): string {
  return BUNDLES[lang]?.[key] ?? en[key] ?? key;
}

// ---- data translations from the API -------------------------------------------------
// entityType -> key -> translated value, for the current language.
type RefBundle = Record<string, Record<string, string>>;
let refCache: Record<string, RefBundle> = {};

export async function loadReference(lang: Lang, token?: string): Promise<RefBundle> {
  if (refCache[lang]) return refCache[lang];
  if (lang === "en") return (refCache[lang] = {});
  try {
    const base = (import.meta as any).env?.VITE_API_BASE ?? "";
    const r = await fetch(`${base}/i18n/reference?lang=${lang}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });
    if (!r.ok) return (refCache[lang] = {});
    const body = await r.json();
    return (refCache[lang] = body.entries ?? {});
  } catch {
    // The console is still perfectly usable in English; a failed translation fetch must
    // not take the page down.
    return (refCache[lang] = {});
  }
}

/**
 * Translate a data value — a shipment state, exception type, severity or role.
 * Returns the original when there is no translation, never blank.
 */
export function tRef(entityType: string, value: string | null | undefined, lang: Lang = getLang()): string {
  if (!value) return "";
  if (lang === "en") return value;
  return refCache[lang]?.[entityType]?.[value] ?? value;
}
