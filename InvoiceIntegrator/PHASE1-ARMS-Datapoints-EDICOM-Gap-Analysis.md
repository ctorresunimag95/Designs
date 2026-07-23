# Phase 1 - ARMS Datapoints vs EDICOM (AE PINT)

Source inputs:

- Workbook: Dubai E Invoice (Questions) - ARMS Comments.xlsx

## Invoice

| #   | Datapoint                     | ARMS Available? | ARMS Mapping/Comment                                                                       | EDICOM Possible Values (if No/Unknown)                                                                         | Definition / Investigation                                                                                                                                                             |
| --- | ----------------------------- | --------------- | ------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Invoice number                | No (Logic)      | Edicomm/ARMS can provide this number based on existing ARMS logic or Dubai regulatory need | Patterned unique ID (for example `INV-YYYY-NNNNN`)                                                             | Must be unique per invoice. Confirm if EDICOM accepts client-generated value only, or can generate server-side.                                                                        |
| 2   | Invoice date                  | Unknown (`-`)   | EDICOM should generate this as invoice generation date                                     | ISO date `YYYY-MM-DD`                                                                                          | Confirm ownership of this value (ARMS vs EDICOM). If EDICOM generates it, verify whether API allows omission.                                                                          |
| 3   | Invoice type code             | No              | Default value, needs CC/OPS input                                                          | `380` (Invoice), `381` (Credit note), and potentially `389`, `261`, `480` if EDICOM supports                   | Coded invoice function type. Confirm exact allowed subset in EDICOM UAE sandbox.                                                                                                       |
| 4   | Invoice currency code         | No              | Default value or logic, needs CC/OPS input                                                 | ISO 4217 (for UAE usually `AED`)                                                                               | Currency for monetary amounts. Confirm if always AED or multi-currency scenarios are allowed.                                                                                          |
| 5   | Invoice transaction type code | No              | Logic to be provided by CC/OPS                                                             | 8-flag string `XXXXXXXX` (0/1 per flag)                                                                        | Bitmask for UAE transaction qualifiers (FTZ, deemed supply, margin scheme, summary, continuous supply, disclosed agent, e-commerce, exports). Confirm exact flag order and validation. |
| 6   | Payment due date              | Yes             | Due date in ARMS                                                                           | -                                                                                                              | Due date field already available in ARMS.                                                                                                                                              |
| 7   | Business process type         | No              | Default value/logic to be provided by CC/OPS                                               | Likely PEPPOL business process identifier (for example `urn:peppol:bis:billing`) and/or P1-P11 profile mapping | Defines invoice business context. Confirm EDICOM expected representation (URN vs coded profile list).                                                                                  |
| 8   | Specification identifier      | No              | Default value/logic to be provided by CC/OPS                                               | `urn:peppol:pint:billing-1@ae-1` (expected AE PINT baseline)                                                   | Declares ruleset/spec used by document. Confirm exact value required by EDICOM in sandbox.                                                                                             |
| 9   | Payment means type code       | No              | Default value/logic to be provided by CC/OPS                                               | `30` (credit transfer), `42` (payment to bank account), `48` (bank card), `49` (direct debit)                  | UNCL4461 settlement method code. Confirm EDICOM accepted list and defaults.                                                                                                            |

## Seller Details

| #   | Datapoint                                 | ARMS Available? | ARMS Mapping/Comment                   | EDICOM Possible Values (if No/Unknown)                                    | Definition / Investigation                                                                 |
| --- | ----------------------------------------- | --------------- | -------------------------------------- | ------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| 10  | Seller name                               | Yes (Logic)     | Default value - BHSI                   | -                                                                         | Legal seller name.                                                                         |
| 11  | Seller electronic address                 | Yes (Logic)     | Derive first 10 digits of BHSI TRN     | -                                                                         | Seller TIN endpoint component (first 10 digits of TRN).                                    |
| 12  | Seller electronic identifier              | Yes (Logic)     | `0235` + first 10 digits of BHSI TRN   | -                                                                         | UAE participant endpoint identifier format.                                                |
| 13  | Seller legal registration identifier      | Unknown (blank) | Default value to be provided by CC/OPS | Trade license no, Emirates ID, Passport no, or Cabinet Decision reference | Official legal entity registration identifier. Confirm business rule by legal entity type. |
| 14  | Seller legal registration identifier type | Yes (Logic)     | Default value - `TL`                   | -                                                                         | Type code: `TL`, `EID`, `PAS`, `CD`.                                                       |
| 15  | Seller tax identifier                     | Yes (Logic)     | Default value - BHSI TRN               | -                                                                         | Seller TRN (15-digit where applicable).                                                    |
| 16  | Seller tax scheme code                    | Yes (Logic)     | Default value - `VAT`                  | -                                                                         | Tax scheme code, usually `VAT`.                                                            |
| 17  | Seller address line 1                     | Yes (Logic)     | Default value - BHSI Office Address    | -                                                                         | Main seller address line.                                                                  |
| 18  | Seller city                               | Yes (Logic)     | Default value - Dubai                  | -                                                                         | Seller city name.                                                                          |
| 19  | Seller country subdivision                | Yes (Logic)     | Default value - Dubai                  | -                                                                         | Emirate/region/province.                                                                   |
| 20  | Seller country code                       | Yes (Logic)     | Default value - `AE`                   | -                                                                         | ISO 3166-1 alpha-2 country code.                                                           |

## Buyer Details

| #   | Datapoint                   | ARMS Available? | ARMS Mapping/Comment                                              | EDICOM Possible Values (if No/Unknown) | Definition / Investigation                                                                                         |
| --- | --------------------------- | --------------- | ----------------------------------------------------------------- | -------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| 21  | Buyer name                  | Yes             | Insured full name / Reinsured full name                           | -                                      | Legal buyer name.                                                                                                  |
| 22  | Buyer electronic address    | Yes (Logic)     | First 10 digits of insured/reinsured TRN                          | -                                      | Buyer endpoint address (TIN-based).                                                                                |
| 23  | Buyer electronic identifier | Yes (Logic)     | `0235` + first 10 digits of insured/reinsured TRN                 | -                                      | Buyer participant identifier.                                                                                      |
| 24  | Buyer tax identifier        | Yes             | Insured TRN or reinsured TRN                                      | -                                      | Buyer TRN where VAT-registered.                                                                                    |
| 25  | Buyer tax scheme code       | Yes (Logic)     | Default value - `VAT`                                             | -                                      | Tax scheme code.                                                                                                   |
| 26  | Buyer address line 1        | Yes             | Insured/reinsured address line 1                                  | -                                      | Main buyer address line.                                                                                           |
| 27  | Buyer city                  | Yes             | Insured city/reinsured city                                       | -                                      | Buyer city name.                                                                                                   |
| 28  | Buyer country subdivision   | Yes             | Insured country/reinsured country                                 | -                                      | Region/subdivision.                                                                                                |
| 29  | Buyer country code          | No              | Mapping needs embedding in ARMS or derivation through ISO mapping | ISO 3166-1 alpha-2 (for UAE `AE`)      | Country code derived from party country. Investigate source country normalization and ISO mapping table ownership. |

## Tax Breakdown

| #   | Datapoint                   | ARMS Available? | ARMS Mapping/Comment                        | EDICOM Possible Values (if No/Unknown) | Definition / Investigation                                                                                                       |
| --- | --------------------------- | --------------- | ------------------------------------------- | -------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| 35  | Tax category taxable amount | Yes (Logic)     | Sum of all tax components                   | -                                      | Taxable base amount for tax category.                                                                                            |
| 36  | Tax category tax amount     | No              | Question: do we need component-wise amount? | Decimal amount (for example `250.00`)  | Tax amount for each tax category. Confirm if category-level breakdown is mandatory when multiple categories exist.               |
| 37  | Tax category code           | No              | Logic to be provided by CC/OPS              | `S`, `Z`, `E`, `AE`, `N`, `O`          | Category code classification (standard, zero, exempt, reverse charge, etc.). Confirm EDICOM allowed subset for UAE PINT profile. |
| 38  | Tax category rate           | No              | Logic to be provided by CC/OPS              | Percentage (for example `5.0`, `0.0`)  | Tax rate percentage bound to category code. Confirm decimal precision and rounding rules.                                        |

## Invoice Line

| #   | Datapoint                       | ARMS Available? | ARMS Mapping/Comment                                | EDICOM Possible Values (if No/Unknown)                     | Definition / Investigation                                                                       |
| --- | ------------------------------- | --------------- | --------------------------------------------------- | ---------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| 39  | Invoice line identifier         | No              | Logic to be provided by CC/OPS                      | Sequence string/number (for example `1`, `2`, `3`)         | Unique line key inside invoice. Confirm if numeric-only required.                                |
| 40  | Invoiced quantity               | Yes (Logic)     | Default value - `1`                                 | -                                                          | Charged quantity.                                                                                |
| 41  | Unit of measure code            | No              | Logic to be provided by CC/OPS                      | UNECE Rec20/21 codes (for example `HUR`, `KGM`, `C62`)     | Unit code tied to quantity. Confirm business default(s) and supported list in EDICOM validation. |
| 42  | Invoice line net amount         | Yes             | Gross Premium                                       | -                                                          | Line amount before tax.                                                                          |
| 43  | Item net price                  | Yes             | Net Premium                                         | -                                                          | Unit net price after discounts.                                                                  |
| 44  | Item gross price                | Yes             | Gross Premium                                       | -                                                          | Unit price before discounts.                                                                     |
| 45  | Item price base quantity        | Yes (Logic)     | Default value - `1`                                 | -                                                          | Quantity basis for unit price.                                                                   |
| 46  | Invoiced item tax category code | No              | Logic to be provided by CC/OPS                      | `S`, `Z`, `E`, `O` (and `AE` where reverse charge applies) | Line-level tax category code. Confirm allowed list for specific policy/tax scenarios.            |
| 47  | Invoiced item tax rate          | No              | Mapping needs embedding in ARMS or derivation logic | Percentage (for example `5.0`, `0.0`)                      | Line-level VAT rate. Investigate source-of-truth and handling for exemptions/zero-rated lines.   |
| 48  | VAT line amount in AED          | Yes             | VAT on premium tax amount in AED                    | -                                                          | VAT amount per line in AED.                                                                      |
| 49  | Invoice line amount in AED      | Yes             | Net premium in AED or jurisdictional                | -                                                          | Total payable line amount in AED.                                                                |
| 50  | Item name                       | Yes             | Policy number                                       | -                                                          | Item short name/title.                                                                           |
| 51  | Item description                | Yes (Logic)     | Concatenation of policy number and gross premium    | -                                                          | Item descriptive narrative.                                                                      |

## Document Totals

| #   | Datapoint                        | ARMS Available? | ARMS Mapping/Comment       | EDICOM Possible Values (if No/Unknown) | Definition / Investigation    |
| --- | -------------------------------- | --------------- | -------------------------- | -------------------------------------- | ----------------------------- |
| 30  | Sum of invoice line net amount   | Yes (Logic)     | Gross premium - tax amount | -                                      | Sum of line net amounts.      |
| 31  | Invoice total amount without tax | Yes             | Gross premium              | -                                      | Total before VAT.             |
| 32  | Invoice total tax amount         | Yes             | Amount for VAT on premium  | -                                      | Total VAT amount.             |
| 33  | Invoice total amount with tax    | Yes (Logic)     | Gross premium - tax amount | -                                      | Grand total including VAT.    |
| 34  | Amount due for payment           | Yes             | Outstanding amount         | -                                      | Amount requested for payment. |


## Clarification For "No ARMS Available" (Phase 1 Values To Use/Consider)

Basis used:

- UAE Electronic Invoice Mandatory Fields v1.0 (23 Feb 2026): business semantic requirements and mandatory fields.
- EDICOM iPaaS Server API: transport/orchestration API (`publish`, schema-based validation, duplicate handling), not the source of business codelists.

Important implementation note:

- iPaaS does not define invoice business values (for example tax category, payment means, invoice type). It validates the payload against the `schema` configured in EDICOM. Therefore, values below should be treated as Phase 1 defaults pending sandbox schema confirmation.

| #   | Datapoint                                      | Phase 1 Recommended Value                                        | Other Values To Consider                                                                                                                    | Clarification                                                                                                                       |
| --- | ---------------------------------------------- | ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Invoice number                                 | Client-generated unique value, format `INV-{YYYY}-{sequence}`    | UUID-based invoice key                                                                                                                      | Must be unique and stable for retries; also use same business reference for idempotent republish behavior in iPaaS flows.           |
| 2   | Invoice date (Unknown)                         | Populate from ARMS posting/issuance date in `YYYY-MM-DD`         | If unavailable, use transmission date with business approval                                                                                | PDF requires invoice date as mandatory; do not rely on iPaaS to derive this unless EDICOM schema explicitly supports omission.      |
| 3   | Invoice type code                              | `380` for standard invoices                                      | `381` for credit note; optionally `389`, `261`, `480` if business scenario applies and schema accepts                                       | Use `380` as default for normal premium invoice. Switch only by explicit business event.                                            |
| 4   | Invoice currency code                          | `AED`                                                            | Other ISO 4217 codes only if policy currency is non-AED and UAE profile permits                                                             | UAE reporting commonly in AED; confirm if source transaction currency can differ while AED amounts remain available where required. |
| 5   | Invoice transaction type code                  | `00000000` as safe baseline                                      | Set specific bits to `1` only when scenario is true (FTZ, deemed supply, margin, summary, continuous, disclosed agent, e-commerce, exports) | Keep baseline all zeros unless tax/compliance confirms exception. This reduces false declarations.                                  |
| 7   | Business process type                          | `urn:peppol:bis:billing`                                         | P1-P11 mapped process profile used by EDICOM tenant                                                                                         | Start with standard billing process unless EDICOM tenant requires a different process identifier mapping.                           |
| 8   | Specification identifier                       | `urn:peppol:pint:billing-1@ae-1`                                 | EDICOM-provided newer AE profile URN/version                                                                                                | Must match target schema validation rule in iPaaS. Treat as tenant-controlled constant.                                             |
| 9   | Payment means type code                        | `30` (credit transfer)                                           | `42`, `48`, `49` when payment method really differs                                                                                         | Use one organization-wide default first, then extend by payment channel mapping.                                                    |
| 13  | Seller legal registration identifier (Unknown) | Seller Trade License number                                      | Emirates ID / Passport / Cabinet Decision ref for special entity types                                                                      | Pair with type at #14. Default to trade license for corporate seller unless legal instructs otherwise.                              |
| 29  | Buyer country code                             | ISO 3166-1 alpha-2 from master data; default `AE` for UAE buyers | Non-AE code for overseas buyers                                                                                                             | Implement deterministic country-name-to-ISO mapping table in ARMS/EDW.                                                              |
| 36  | Tax category tax amount                        | Calculate and send per tax category bucket                       | Single bucket only if one tax category exists                                                                                               | Mandatory in PDF. For mixed tax categories, send one breakdown per category with matching taxable amount/rate/code.                 |
| 37  | Tax category code                              | `S` (standard rate) as default                                   | `Z`, `E`, `O`, `AE`, `N` by tax scenario                                                                                                    | Select code from tax engine/compliance rules, not hardcoded by product alone.                                                       |
| 38  | Tax category rate                              | `5.00` for standard UAE VAT                                      | `0.00` for zero/exempt/out-of-scope; other rates only if regulation applies                                                                 | Rate must align with category code and taxable base logic.                                                                          |
| 39  | Invoice line identifier                        | Sequential integers as strings: `1`, `2`, `3`                    | Stable source line IDs if already available                                                                                                 | Ensure uniqueness per invoice and deterministic order for reconciliation.                                                           |
| 41  | Unit of measure code                           | `C62` (one/unit) for policy-level service line                   | `HUR` for hour-based service, `KGM` for weight-based goods, other UNECE Rec20/21 codes as needed                                            | For insurance/service invoices, `C62` is usually safest when quantity defaults to 1.                                                |
| 46  | Invoiced item tax category code                | Mirror header/category default `S`                               | `Z`, `E`, `O`, `AE`, `N` by line-level tax treatment                                                                                        | Line code should match tax breakdown logic; avoid mismatches between line and summary.                                              |
| 47  | Invoiced item tax rate                         | `5.00` where code is `S`                                         | `0.00` for `Z`/`E`/`O`; scenario-specific rate otherwise                                                                                    | Keep precision consistent across line, category, and totals calculations.                                                           |

---

## Phase 1 Deliverable: AE PINT Payload Builder Component

**Goal:** Build a component (`EdicomPayloadMapper` + supporting services) that transforms ARMS datapoints into a fully-formed AE PINT JSON payload matching the contract in [EDICOM-Info.md](EDICOM-Info.md).

**Inputs:**
- Invoice master data (invoice number, date, type)
- Seller details (name, TRN, address — all from BHSI/org defaults)
- Buyer details (insured/reinsured name, TRN, address)
- Line items (premium, tax, description from policy)
- Tax breakdown (VAT components by category)

**Output:** Fully-formed JSON structure per AE PINT 51-field spec, validated and ready to submit to EDICOM.

### Sample Output — Insurance Premium Invoice

The component will produce a JSON structure like this. Values below reflect realistic ARMS/insurance data mappings discovered during Phase 1 investigation:

```json
{
  "invoiceNumber": "INV-2026-000847",
  "issueDate": "2026-07-23",
  "invoiceTypeCode": "380",
  "currencyCode": "AED",
  "invoiceTransactionTypeCode": "00000000",
  "dueDate": "2026-08-23",
  "businessProcessType": "urn:peppol:bis:billing",
  "specificationIdentifier": "urn:peppol:pint:billing-1@ae-1",
  "paymentMeansCode": "30",

  "seller": {
    "name": "BHSI Insurance Brokers LLC",
    "electronicDelivery": {
      "address": "1001234567",
      "identifier": "0235"
    },
    "legalInfo": {
      "identifier": "TL-DXB-456789",
      "registrationIdentifierType": "TL"
    },
    "taxInfo": {
      "identifier": "100123456700003",
      "scheme": "VAT"
    },
    "address": {
      "line1": "Office 101, Business Centre, DIFC",
      "city": "Dubai",
      "subdivision": "Dubai",
      "countryCode": "AE"
    }
  },

  "buyer": {
    "name": "Acme Manufacturing FZE",
    "electronicDelivery": {
      "address": "1009876543",
      "identifier": "0235"
    },
    "taxInfo": {
      "identifier": "100987654300003",
      "scheme": "VAT"
    },
    "address": {
      "line1": "Unit 12, Jebel Ali Free Zone",
      "city": "Dubai",
      "subdivision": "Dubai",
      "countryCode": "AE"
    }
  },

  "taxBreakdown": {
    "taxableAmount": 50000.0,
    "taxAmount": 2500.0,
    "taxClass": {
      "code": "S",
      "rate": 5.0
    }
  },

  "lines": [
    {
      "lineIdentifier": "1",
      "invoicedQuantity": 1,
      "unitOfMeasureCode": "C62",
      "lineNetAmount": 50000.0,
      "unitPrice": {
        "net": 50000.0,
        "gross": 50000.0,
        "baseQuantity": 1
      },
      "taxClass": {
        "code": "S",
        "rate": 5.0
      },
      "vatLineAmountAED": 2500.0,
      "lineAmountAED": 52500.0,
      "itemName": "POL-2026-08847",
      "itemDescription": "Annual property and casualty insurance policy for Acme Manufacturing FZE — coverage period 2026-07-23 to 2027-07-22"
    }
  ],

  "totals": {
    "sumOfLineNetAmounts": 50000.0,
    "invoiceTotalWithoutTax": 50000.0,
    "invoiceTotalTaxAmount": 2500.0,
    "invoiceTotalWithTax": 52500.0,
    "amountDueForPayment": 52500.0
  }
}
```

**Key mappings from Phase 1 investigation:**
- **Invoice number** → Generated per org logic or source system; formatted `INV-YYYY-NNNNNN`
- **Invoice date** → ARMS posting/issuance date
- **Seller** → BHSI org defaults (name, TRN, address, trade license)
- **Buyer** → Insured/reinsured party from policy master
- **Lines** → One line per invoice, quantity = 1, unit = `C62` (one/unit)
- **Item name** → Policy number
- **Item description** → Policy number + coverage details
- **Tax** → Standard VAT at 5% on gross premium; code = `S` (standard rate)
- **Totals** → Sum of gross premium, VAT, and grand total

**Phase 1 validation checklist:**
- ✅ All 51 mandatory fields populated from ARMS sources or defaults
- ✅ Tax calculation matches invoice totals (no rounding drift > 0.01 AED)
- ✅ Buyer country code resolved via ISO mapping table
- ✅ Tax category code and rate align (no `S` with 0% or `Z` with 5%)
- ✅ Line amounts + tax = totals (cross-field reconciliation)
- ✅ No missing or null fields in terminal JSON
