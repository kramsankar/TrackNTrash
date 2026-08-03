# Multilingual support

Tamil and Hindi alongside English, across the API, the console and the three handheld apps.
Adding a further Indian language is mostly data entry, not code.

---

## 1. The rule everything follows

**A missing translation falls back to English. Nothing ever renders blank.**

A picker who switches to Tamil and finds half the screen empty is worse off than one reading
English — blank fields look like missing *data*, not a missing *translation*, and that costs
trust in the system rather than in the localisation. Clearing a translation therefore removes
the row rather than storing an empty string, which restores the English value.

---

## 2. Where each kind of text lives

| Kind | Example | Source | Why |
|---|---|---|---|
| **Data** | store name, checkpoint, shipment state, exception type | `ops.Translation` via the API | It is data. It changes without a release. |
| **UI chrome** | menu labels, buttons, headings | bundled in each client | Must render before any request completes |

That split is deliberate. A handheld on warehouse Wi-Fi drifts in and out of coverage all
shift; an app whose buttons only appear once a call succeeds fails exactly when the network
does. Conversely, a store's name is not something to redeploy three apps to change.

`ops.Translation` reserves `EntityType = 'ui'` for server-driven overrides later, but nothing
depends on it today.

---

## 3. Data model

```
ref.Language      LanguageCode PK, EnglishName, NativeName, IsActive, SortOrder
ops.Translation   EntityType, EntityKey, FieldName, LanguageCode, Value
                  UNIQUE (EntityType, EntityKey, FieldName, LanguageCode)
```

`NameTa` / `NameHi` columns were the alternative and would mean a migration per language per
table. A chain operating across several states would pay that repeatedly; here a new language
is rows in two tables.

**`EntityKey` is the natural key** — `StoreCode`, `ZoneCode`, `CheckpointCode` — not an
identity id. Identity values differ between environments and change on a re-seed; a store code
does not.

### Unicode

Nothing needed converting. Every human-readable column was already `NVARCHAR`, and a Tamil
round-trip through the live API came back byte-identical. The `VARCHAR` columns that remain
(`Status`, `Verdict`, `Gtin`, `ExceptionType`) are enum codes the code compares against, not
text anyone reads, and are deliberately left as ASCII.

> `sqlcmd` prints `??????` for Indic text. That is its console encoding, not the stored data.
> Verify by code point — `SELECT UNICODE(Value)` — or through the API, never by eye. Apply
> migrations containing Indic literals with `-f 65001`.

---

## 4. API

| Endpoint | Purpose |
|---|---|
| `GET /i18n/languages` | active languages with native names |
| `GET /i18n/reference?lang=ta` | checkpoints, states, exception types, severities, roles |
| `GET /i18n/translations/{type}/{key}` | every translation for one record |
| `PUT /i18n/translations` | upsert one; empty value clears it |
| `GET /masters/{key}?lang=ta` | master rows with display fields overlaid |

**Language resolves once, server-side**, in this order:

1. explicit `?lang=`
2. `Accept-Language`, including weighted lists and region subtags (`ta-IN,ta;q=0.9`)
3. English

An unknown code resolves to English rather than returning 4xx. A stale preference on an old
handset should degrade to a readable screen, not a failed request.

Localised master reads also return the untranslated value as `nameOriginal`, so an editor can
see what a translation is standing in for.

### Adding a language

```sql
INSERT INTO ref.Language (LanguageCode, EnglishName, NativeName, SortOrder)
VALUES ('kn', N'Kannada', N'ಕನ್ನಡ', 40);
```

Then add translation rows. No schema change, no redeploy. Add the same code to the clients'
bundles for the UI chrome.

---

## 5. Fonts — the trap worth knowing

**The handheld apps pinned `FontFamily="OpenSansRegular"` in 30 style setters. OpenSans
contains 0 of 128 Tamil code points and 0 of 128 Devanagari.** Verified with fontTools, not
assumed.

On Android a *pinned custom typeface carries no fallback chain*, so a missing glyph renders as
a tofu box rather than being substituted. Every localised label would have shipped as boxes.

The fix is to pin nothing. The system face has the fallback chain and Android has shipped Noto
Sans Tamil and Devanagari since API 21; the apps target 8.0+. If a specific typeface is ever
wanted again, bundle Noto Sans Tamil and Devanagari and select per language — do not reinstate
a Latin-only pin.

The browser has no such problem: measured text widths for Tamil and Devanagari came back as
four distinct values, none near the `.notdef` control, so glyphs are genuinely drawn.

---

## 6. What is translated today

**Seeded** (70 rows across Tamil and Hindi): the four checkpoints, six shipment states plus
four terminal ones, all eleven exception types, four severities, six roles.

**Available but not yet populated**: master records. The mechanism works — `PUT
/i18n/translations` against `store`, `product`, `zone`, `rack`, `vehicle` — but real store and
product names are customer data and have to be entered by someone who knows them.

### Not translated, and why

**Exception `Detail` strings.** These are assembled in C# as English prose:

```csharp
Detail = $"Item count {verdict} on carton {cartonId}: {detail}"
```

Translating those properly means replacing the strings with message keys plus arguments, so
the words and the values are separated. A translation table cannot help with free text built
at runtime. Until that refactor, the exception *type* and *severity* localise but the detail
sentence stays English — the type is what the board sorts and filters on, so the useful part
is covered.

---

## 7. Verifying

The integration suite has 14 checks covering this. The ones that matter most:

- an untranslated field returns its English value, not blank
- clearing a translation restores English
- an unknown language code returns 200 in English, not an error
- the English view is unaffected by a translation existing
- native names are entirely inside the correct Unicode block
- no seeded row was mangled to question marks

Native-name checking by block matters because **hand-computing code points produced
`౜౦ிྜ்` on the first attempt** — a Telugu and a Tibetan character spliced into what should have
been தமிழ். Use literals in a UTF-8 file, not `NCHAR()`. The `MERGE` that seeds languages also
has to include `NativeName` in its `UPDATE`, or a correction never lands on re-run.
