# EDICOM UAE e-Invoicing — Integration Reference

> **Status:** Research snapshot — based on EDICOM's public materials, the AE PINT / Peppol PINT specification, and known Peppol Access Point patterns. Items marked ⚠️ must be confirmed against EDICOM's actual sandbox documentation once received.

---

## What EDICOM Is

EDICOM is an officially certified **Accredited Service Provider (ASP)** and **Peppol Access Point** for the UAE e-invoicing mandate. It acts as Corner 2 in the 5-corner Peppol model: your system sends structured invoice data to EDICOM, and EDICOM handles UBL conversion, routing to the buyer's Access Point, and tax reporting to the UAE FTA.

---

## Integration Flow (5-Corner Model)

```
Your system
   │
   │  POST /invoices  (REST + JSON or UBL XML)
   ▼
EDICOM — Corner 2 (your Access Point)
   │  1. Validates payload against AE PINT rules
   │  2. Converts to UBL 2.1 XML (if you sent JSON)
   │  3. Wraps in AS4 envelope, signs, routes via Peppol SMP
   ▼
Buyer's Access Point — Corner 3
   ▼
Buyer — Corner 4
   ▼
UAE FTA tax platform — Corner 5 (government reporting; no content validation response)
```

**EDICOM returns:** an immediate HTTP response with a `transactionId`, then an async status (Acknowledged / Rejected) reachable via a status-poll endpoint.

---

## Authentication ⚠️

EDICOM uses **OAuth2 Client Credentials**:

```http
POST https://auth.edicomgroup.com/oauth2/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
&client_id=YOUR_CLIENT_ID
&client_secret=YOUR_CLIENT_SECRET
&scope=einvoicing:submit einvoicing:status
```

Response:

```json
{
  "access_token": "eyJ...",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

All subsequent calls use `Authorization: Bearer {access_token}`.

> ⚠️ Confirm: exact token URL, scope names, whether mTLS is required for the UAE regulated channel.

---

## Invoice Submission ⚠️

```http
POST https://api.edicomgroup.com/einvoicing/v1/invoices
Authorization: Bearer {access_token}
Content-Type: application/json
Idempotency-Key: {invoice-number-or-uuid}
```

> ⚠️ Confirm: exact base URL, API version path, Idempotency-Key header name and retry semantics.

---

## JSON Payload Contract (AE PINT mandatory fields)

The UAE mandate is **AE PINT** (Peppol PINT adapted for UAE/FTA). The fields below are derived from the official UAE Electronic Invoice Mandatory Fields spec (MoF v1.0, Feb 2026) and map to UBL 2.1 BT/BG elements. EDICOM likely accepts a JSON representation which they convert to UBL — the field names in their actual contract may differ (camelCase, snake_case, or proprietary wrapper). ⚠️

```json
{
  // ── Invoice Details (MoF §4.1 fields 1–9) ──────────────────────────────
  "invoiceNumber": "INV-2025-00123",          // #1  Invoice number
  "issueDate": "2025-11-15",                  // #2  Invoice date
  "invoiceTypeCode": "380",                   // #3  380 = Invoice, 381 = Credit Note
  "currencyCode": "AED",                      // #4  ISO 4217
  "invoiceTransactionTypeCode": "00000000",   // #5  8-flag bitmask: FreeTradeZone/DeemedSupply/MarginScheme/Summary/ContinuousSupply/DisclosedAgent/eCommerce/Exports
  "dueDate": "2025-12-15",                    // #6  Payment due date
  "businessProcessType": "urn:peppol:bis:billing",           // #7  Business process type / BT-23
  "specificationIdentifier": "urn:peppol:pint:billing-1@ae-1", // #8  Specification Identifier / BT-24
  "paymentMeansCode": "30",                   // #9  UNCL4461: 30 = Credit transfer

  // ── Seller Details (MoF §4.1 fields 10–20) ─────────────────────────────
  "seller": {
    "name": "Acme FZE",                       // #10 Full legal name
    "electronicDelivery": {                   // #11–12 Electronic delivery address
      "address": "1001234567",                // #11 Seller TIN (first 10 digits of TRN)
      "identifier": "0235"                    // #12 Fixed scheme code for UAE businesses
    },
    "legalInfo": {                            // #13–14 Legal registration document
      "identifier": "TL-DXB-123456",          // #13 Trade license / EID / passport number
      "registrationIdentifierType": "TL"      // #14 TL / EID / PAS / CD
    },
    "taxInfo": {                              // #15–16 Tax registration
      "identifier": "100123456700003",        // #15 TRN (15 digits); use TIN if no TRN
      "scheme": "VAT"                         // #16 Default VAT
    },
    "address": {                              // #17–20
      "line1": "Office 101, Building A",      // #17
      "city": "Dubai",                        // #18
      "subdivision": "Dubai",                 // #19 Emirate / region
      "countryCode": "AE"                     // #20
    }
  },

  // ── Buyer Details (MoF §4.1 fields 21–29) ──────────────────────────────
  "buyer": {
    "name": "Buyer Corp LLC",                 // #21 Full legal name
    "electronicDelivery": {                   // #22–23 Electronic delivery address
      "address": "1009876543",                // #22 Buyer TIN — delivery endpoint
      "identifier": "0235"                    // #23 Fixed scheme code for UAE businesses
    },
    "taxInfo": {                              // #24–25 Tax registration
      "identifier": "100987654300003",        // #24 Buyer TRN (if VAT-registered)
      "scheme": "VAT"                         // #25 Default VAT
    },
    "address": {                              // #26–29
      "line1": "Unit 5, Free Zone",           // #26
      "city": "Abu Dhabi",                    // #27
      "subdivision": "Abu Dhabi",             // #28 Emirate / region
      "countryCode": "AE"                     // #29
    }
  },

  // ── Tax Breakdown (MoF §4.1 fields 35–38) ──────────────────────────────
  "taxBreakdown": {
    "taxableAmount": 5000.0,                  // #35 Sum of taxable amounts for this category
    "taxAmount": 250.0,                       // #36 Tax amount for this category
    "taxClass": {                             // #37–38 Tax category classification
      "code": "S",                            // #37 S = Standard, Z = Zero-rated, E = Exempt
      "rate": 5.0                             // #38 Percentage (UAE standard = 5%)
    }
  },

  // ── Invoice Lines (MoF §4.1 fields 39–51) ──────────────────────────────
  "lines": [
    {
      "lineIdentifier": "1",                  // #39
      "invoicedQuantity": 10,                 // #40
      "unitOfMeasureCode": "HUR",             // #41 UN/ECE Rec 20 unit code
      "lineNetAmount": 5000.0,                // #42 Line total before tax
      "unitPrice": {                          // #43–45 Item pricing
        "net": 500.0,                         // #43 Unit price after discount
        "gross": 500.0,                       // #44 Unit price before discount
        "baseQuantity": 1                     // #45 Units the price applies to
      },
      "taxClass": {                           // #46–47 Line-level tax classification
        "code": "S",                          // #46 S / Z / E
        "rate": 5.0                           // #47 Percentage
      },
      "vatLineAmountAED": 250.0,              // #48 VAT amount for this line in AED
      "lineAmountAED": 5250.0,                // #49 Total payable for this line in AED
      "itemName": "Consulting services",      // #50
      "itemDescription": "Monthly consulting engagement" // #51
    }
  ],

  // ── Document Totals (MoF §4.1 fields 30–34) ────────────────────────────
  "totals": {
    "sumOfLineNetAmounts": 5000.0,            // #30 Sum of all line net amounts
    "invoiceTotalWithoutTax": 5000.0,         // #31
    "invoiceTotalTaxAmount": 250.0,           // #32
    "invoiceTotalWithTax": 5250.0,            // #33
    "amountDueForPayment": 5250.0             // #34
  }
}
```

---

## Status Polling ⚠️

After submission, poll with the returned `transactionId`:

```http
GET https://api.edicomgroup.com/einvoicing/v1/invoices/{transactionId}/status
Authorization: Bearer {access_token}
```

Response:

```json
{
  "transactionId": "EDICOM-TXN-ABC123",
  "invoiceNumber": "INV-2025-00123",
  "status": "ACKNOWLEDGED",
  "edicomReference": "UAE-2025-XYZ",
  "processedAt": "2025-11-15T10:32:00Z",
  "errors": []
}
```

**Status values:** `PENDING` → `ACKNOWLEDGED` (terminal ✅) | `REJECTED` (terminal ❌)

On `REJECTED`, `errors[]` contains AE PINT validation codes (e.g. `PEPPOL-EN16931-R001`).

---

## Open Questions to Confirm with EDICOM

| #   | Question                                                                                                                                                                         |
| --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Exact REST endpoint URLs and API versioning scheme                                                                                                                               |
| 2   | Whether they accept **JSON** (preferred) or require pre-built **UBL 2.1 XML**                                                                                                    |
| 3   | Exact JSON field names / schema (their wrapper may differ from the shape above)                                                                                                  |
| 4   | OAuth2 token URL, scope names, and whether mTLS is also required                                                                                                                 |
| 5   | `Idempotency-Key` header name and resubmission behaviour on ambiguous timeouts                                                                                                   |
| 6   | Whether buyer Peppol ID is mandatory or EDICOM resolves it via SMP lookup                                                                                                        |
| 7   | Error code taxonomy — which HTTP codes / error codes are transient vs. permanent                                                                                                 |
| 8   | Rate limits, `Retry-After` header semantics, recommended batch size                                                                                                              |
| 9   | Who assigns the EDICOM reference/UUID — us or them — and when it is returned                                                                                                     |
| 10  | Sandbox base URL and how to obtain test credentials                                                                                                                              |
| 11  | Polling mechanism for status updates — whether webhooks are supported or only polling, what limit they have to mark an invoice as stale or errored after some time of inactivity |

---

## References

- [EDICOM — UAE Electronic Invoicing](https://edicomgroup.com/electronic-invoicing/united-arab-emirates)
- [Peppol BIS Billing 3.0](https://docs.peppol.eu/poacc/billing/3.0/)
