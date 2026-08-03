/* =====================================================================================
   Migration 010 — Indian-language support. Tamil and Hindi to start.

   Two tables rather than per-language columns. Adding NameTa / NameHi to every master would
   mean a migration per language per table, and the retail chains this serves operate across
   more than two states. Here, a new language is a row in ref.Language plus translation rows —
   no schema change, no redeploy.

   Nothing needs converting for Unicode: every human-readable column is already NVARCHAR, and
   a round-trip of Tamil through the live API came back byte-identical. The VARCHAR columns
   that remain are enum codes (Status, Verdict, GTIN) and are deliberately left as ASCII —
   they are identifiers the code compares against, not text anyone reads.

   Idempotent.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------------------
   Languages on offer. NativeName is what the picker shows: a Tamil speaker looks for
   "தமிழ்", not "Tamil".
   --------------------------------------------------------------------------------- */
IF OBJECT_ID('ref.Language', 'U') IS NULL
BEGIN
    CREATE TABLE ref.Language
    (
        LanguageCode VARCHAR(10)   NOT NULL CONSTRAINT PK_Language PRIMARY KEY,
        EnglishName  NVARCHAR(60)  NOT NULL,
        NativeName   NVARCHAR(60)  NOT NULL,
        IsActive     BIT           NOT NULL CONSTRAINT DF_Language_Active DEFAULT 1,
        SortOrder    INT           NOT NULL CONSTRAINT DF_Language_Sort DEFAULT 100
    );
    PRINT 'Created ref.Language';
END
ELSE PRINT 'ref.Language already exists';
GO

-- Literals, not NCHAR(). Hand-computing code points put a Telugu and a Tibetan character
-- into the Tamil name on the first attempt; the file is applied with -f 65001 so the UTF-8
-- literals arrive intact, which is both safer and readable by someone who speaks the language.
MERGE ref.Language AS t
USING (VALUES
    ('en', N'English', N'English', 1, 10),
    ('ta', N'Tamil',   N'தமிழ்',  1, 20),
    ('hi', N'Hindi',   N'हिन्दी', 1, 30)
) AS s(LanguageCode, EnglishName, NativeName, IsActive, SortOrder)
ON t.LanguageCode = s.LanguageCode
-- NativeName must be in the UPDATE too, or a correction to it never lands on re-run.
WHEN MATCHED THEN UPDATE SET EnglishName = s.EnglishName, NativeName = s.NativeName, SortOrder = s.SortOrder
WHEN NOT MATCHED THEN
    INSERT (LanguageCode, EnglishName, NativeName, IsActive, SortOrder)
    VALUES (s.LanguageCode, s.EnglishName, s.NativeName, s.IsActive, s.SortOrder);
GO

/* ---------------------------------------------------------------------------------
   Generic translations. EntityKey is the natural key as text (StoreCode, ZoneCode,
   CheckpointCode) rather than an identity id, so a translation survives a row being
   re-seeded and does not depend on identity values matching across environments.

   This table holds *data* — master records and reference values. UI chrome (button labels,
   headings) is bundled into each client instead: a handheld on warehouse Wi-Fi must render
   its own buttons without a round trip, and a picker cannot be stuck at a blank screen
   because the strings endpoint was unreachable. EntityType 'ui' is left available for
   server-driven overrides later, but nothing depends on it today.
   --------------------------------------------------------------------------------- */
IF OBJECT_ID('ops.Translation', 'U') IS NULL
BEGIN
    CREATE TABLE ops.Translation
    (
        TranslationId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Translation PRIMARY KEY,
        EntityType    VARCHAR(40)   NOT NULL,   -- 'store' | 'product' | 'checkpoint' | 'ui' ...
        EntityKey     NVARCHAR(100) NOT NULL,   -- natural key, or the UI string key
        FieldName     VARCHAR(40)   NOT NULL,   -- 'name' | 'description' | 'text'
        LanguageCode  VARCHAR(10)   NOT NULL
            CONSTRAINT FK_Translation_Language FOREIGN KEY REFERENCES ref.Language(LanguageCode),
        Value         NVARCHAR(1000) NOT NULL,
        UpdatedUtc    DATETIME2(3)  NOT NULL CONSTRAINT DF_Translation_Utc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UX_Translation UNIQUE (EntityType, EntityKey, FieldName, LanguageCode)
    );
    -- The hot read is "every string for this language", for a UI bundle.
    CREATE INDEX IX_Translation_Lang ON ops.Translation (LanguageCode, EntityType)
        INCLUDE (EntityKey, FieldName, Value);
    PRINT 'Created ops.Translation';
END
ELSE PRINT 'ops.Translation already exists';
GO

/* ---------------------------------------------------------------------------------
   Reference data first — checkpoints and states appear on every screen and in every
   exception, so translating them buys more than any master.
   --------------------------------------------------------------------------------- */
DECLARE @t TABLE (EntityType VARCHAR(40), EntityKey NVARCHAR(100), FieldName VARCHAR(40),
                  LanguageCode VARCHAR(10), Value NVARCHAR(1000));

-- Checkpoints
INSERT INTO @t VALUES
 ('checkpoint','PickTrayBuild','name','ta', N'எடுத்தல் மற்றும் தட்டு தயாரிப்பு'),
 ('checkpoint','PickTrayBuild','name','hi', N'पिकिंग और ट्रे निर्माण'),
 ('checkpoint','DispatchDock','name','ta',  N'அனுப்பும் நிலையம்'),
 ('checkpoint','DispatchDock','name','hi',  N'प्रेषण डॉक'),
 ('checkpoint','VehicleLoad','name','ta',   N'வாகனத்தில் ஏற்றுதல்'),
 ('checkpoint','VehicleLoad','name','hi',   N'वाहन लोडिंग'),
 ('checkpoint','StoreReceive','name','ta',  N'கடையில் பெறுதல்'),
 ('checkpoint','StoreReceive','name','hi',  N'स्टोर प्राप्ति');

-- Shipment states
INSERT INTO @t VALUES
 ('state','Ordered','name','ta',      N'ஆர்டர் செய்யப்பட்டது'),
 ('state','Ordered','name','hi',      N'ऑर्डर किया गया'),
 ('state','Picked','name','ta',       N'எடுக்கப்பட்டது'),
 ('state','Picked','name','hi',       N'चुना गया'),
 ('state','Staged','name','ta',       N'தயார் நிலையில்'),
 ('state','Staged','name','hi',       N'तैयार'),
 ('state','Loaded','name','ta',       N'ஏற்றப்பட்டது'),
 ('state','Loaded','name','hi',       N'लोड किया गया'),
 ('state','InTransit','name','ta',    N'பயணத்தில்'),
 ('state','InTransit','name','hi',    N'रास्ते में'),
 ('state','Received','name','ta',     N'பெறப்பட்டது'),
 ('state','Received','name','hi',     N'प्राप्त हुआ'),
 ('state','ShortShipped','name','ta', N'குறைவாக அனுப்பப்பட்டது'),
 ('state','ShortShipped','name','hi', N'कम भेजा गया'),
 ('state','Damaged','name','ta',      N'சேதமடைந்தது'),
 ('state','Damaged','name','hi',      N'क्षतिग्रस्त'),
 ('state','WrongStore','name','ta',   N'தவறான கடை'),
 ('state','WrongStore','name','hi',   N'गलत स्टोर'),
 ('state','Lost','name','ta',         N'தொலைந்தது'),
 ('state','Lost','name','hi',         N'खोया हुआ');

-- Exception types. These head every row on the exception board.
INSERT INTO @t VALUES
 ('exception','CountMismatch','name','ta',     N'எண்ணிக்கை பொருந்தவில்லை'),
 ('exception','CountMismatch','name','hi',     N'गिनती मेल नहीं खाती'),
 ('exception','UnknownCarton','name','ta',     N'அறியப்படாத பெட்டி'),
 ('exception','UnknownCarton','name','hi',     N'अज्ञात कार्टन'),
 ('exception','MissingCarton','name','ta',     N'காணாமல் போன பெட்டி'),
 ('exception','MissingCarton','name','hi',     N'गुम कार्टन'),
 ('exception','WrongTrip','name','ta',         N'தவறான பயணம்'),
 ('exception','WrongTrip','name','hi',         N'गलत ट्रिप'),
 ('exception','WrongStore','name','ta',        N'தவறான கடை'),
 ('exception','WrongStore','name','hi',        N'गलत स्टोर'),
 ('exception','IllegalTransition','name','ta', N'வரிசை மீறல்'),
 ('exception','IllegalTransition','name','hi', N'अनुक्रम उल्लंघन'),
 ('exception','TrayDwellExceeded','name','ta', N'தட்டு தேக்கம் அதிகம்'),
 ('exception','TrayDwellExceeded','name','hi', N'ट्रे ठहराव अधिक'),
 ('exception','NoReceiveSla','name','ta',      N'பெறுதல் காலக்கெடு மீறல்'),
 ('exception','NoReceiveSla','name','hi',      N'प्राप्ति समय-सीमा उल्लंघन'),
 ('exception','SuspectedLost','name','ta',     N'தொலைந்ததாக ஐயம்'),
 ('exception','SuspectedLost','name','hi',     N'खोया होने का संदेह'),
 ('exception','Damaged','name','ta',           N'சேதம்'),
 ('exception','Damaged','name','hi',           N'क्षतिग्रस्त'),
 ('exception','ShortShipped','name','ta',      N'குறைவாக அனுப்பப்பட்டது'),
 ('exception','ShortShipped','name','hi',      N'कम भेजा गया');

-- Severities
INSERT INTO @t VALUES
 ('severity','Critical','name','ta', N'மிக முக்கியம்'),
 ('severity','Critical','name','hi', N'अत्यंत गंभीर'),
 ('severity','High','name','ta',     N'அதிகம்'),
 ('severity','High','name','hi',     N'उच्च'),
 ('severity','Medium','name','ta',   N'நடுத்தரம்'),
 ('severity','Medium','name','hi',   N'मध्यम'),
 ('severity','Low','name','ta',      N'குறைவு'),
 ('severity','Low','name','hi',      N'निम्न');

-- Roles
INSERT INTO @t VALUES
 ('role','Admin','name','ta',            N'நிர்வாகி'),
 ('role','Admin','name','hi',            N'प्रशासक'),
 ('role','WarehouseManager','name','ta', N'கிடங்கு மேலாளர்'),
 ('role','WarehouseManager','name','hi', N'गोदाम प्रबंधक'),
 ('role','Dispatcher','name','ta',       N'அனுப்புநர்'),
 ('role','Dispatcher','name','hi',       N'प्रेषक'),
 ('role','Picker','name','ta',           N'எடுப்பவர்'),
 ('role','Picker','name','hi',           N'पिकर'),
 ('role','StoreManager','name','ta',     N'கடை மேலாளர்'),
 ('role','StoreManager','name','hi',     N'स्टोर प्रबंधक'),
 ('role','CameraDevice','name','ta',     N'கேமரா சாதனம்'),
 ('role','CameraDevice','name','hi',     N'कैमरा उपकरण');

MERGE ops.Translation AS tgt
USING @t AS src
ON  tgt.EntityType = src.EntityType AND tgt.EntityKey = src.EntityKey
AND tgt.FieldName = src.FieldName AND tgt.LanguageCode = src.LanguageCode
WHEN MATCHED THEN UPDATE SET Value = src.Value, UpdatedUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (EntityType, EntityKey, FieldName, LanguageCode, Value)
    VALUES (src.EntityType, src.EntityKey, src.FieldName, src.LanguageCode, src.Value);
GO

PRINT 'Migration 010 complete.';
GO
