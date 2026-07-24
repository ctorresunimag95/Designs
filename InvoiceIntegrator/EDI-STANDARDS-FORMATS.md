# EDI Standards & Format Reference

> **Context:** EDICOM is primarily an EDI/integration platform. If the `/publish` endpoint expects a **file** rather than JSON, invoices must be serialized into a structured EDI document format (not raw JSON fields). This document outlines the three main EDI standards and how your 51-point insurance premium invoice would be represented in each.

---

## Overview: What is EDI?

**EDI = Electronic Data Interchange** — a standardized format for structured business documents. Unlike JSON or XML that are generic data formats, EDI is **domain-specific**, with fixed segments and fields optimized for B2B supply chain, retail, and invoice workflows.

**Key characteristics:**

- Fixed-format or delimiter-based text structure (not human-readable)
- Industry-standard codes for segments, fields, and values
- Globally recognized and mandated by many enterprises
- Used for invoices, purchase orders, shipments, payments, etc.

**Why EDICOM uses it:** As an iPaaS (integration platform as a service), EDICOM's core value is translating between EDI standards and various destinations. They likely expect native EDI documents so they can validate, transform, and route to downstream systems.

---

## Standard 1: X12 (ANSI X12)

**Scope:** North American standard (US, Canada, Mexico)  
**Official Reference:** [ANSI X12 Standards — ASC X12](https://www.x12.org/)  
**Invoice Format:** Transaction Set **810** (Invoice)

### How it works

X12 uses **segments** (lines starting with segment IDs like `ISA`, `GS`, `ST`, `BIG`, etc.) with field separators (typically `*`) and segment terminators (`~`).

Example structure for your insurance premium invoice:

```
ISA*00*          *00*          *ZZ*SENDER-ID       *ZZ*RECEIVER-ID     *260723*1030*U*00401*000000001*0*P*:~
GS*IN*SENDER-ID*RECEIVER-ID*20260723*103000*1*X*004010~
ST*810*0001~
BIG*20260723*INV-2026-000847*20260723~
N1*ST*Acme Manufacturing FZE~
N3*Unit 12, Jebel Ali Free Zone~
N4*Dubai*Dubai*AE~
N1*SF*BHSI Insurance Brokers LLC~
N3*Office 101, Business Centre, DIFC~
N4*Dubai*Dubai*AE~
IT1*1*1*C62*50000.00***S*5.0~
IT1*2*1*C62*52500.00***S*5.0~
TDS*2500.00*50000.00~
CAD*D*S*5.00*2500.00*50000.00~
CTT*2~
SE*15*0001~
GS*IN*SENDER-ID*RECEIVER-ID*20260723*103000*1*X*004010~
```

**Key segments for your invoice:**
| Segment | Purpose | Your Values |
|---------|---------|-------------|
| ISA | Interchange header (auth/security) | Sender/Receiver IDs, timestamp |
| GS | Functional group header | Transaction type (IN = Invoice) |
| ST | Transaction set header | Set type (810) and control #|
| BIG | Beginning of invoice | Invoice #, date, due date |
| N1 | Name/address | Buyer, seller details |
| N3 | Address line 1 | Street address |
| N4 | City/state/zip | City, Emirate, Country |
| IT1 | Item line | Line #, qty, UOM, price, tax info |
| TDS | Tax total/summary | Tax amount, net amount |
| CAD | Tax detail | Tax category, rate, amount |
| CTT | Line item count | Total line count |
| SE | Transaction set trailer | Control # |

**UAE Insurance Premium Invoice in X12 810:**

```
ISA*00*          *00*          *ZZ*BHSI0101      *ZZ*RECEIVER1      *260723*1030*U*00401*000000001*0*P*:~
GS*IN*BHSI0101*RECEIVER1*20260723*103000*1*X*004010~
ST*810*000001~
BIG*20260723*INV-2026-000847*20260723*20260823~
N1*ST*Acme Manufacturing FZE*85*1009876543~
N3*Unit 12, Jebel Ali Free Zone~
N4*Dubai*Dubai*UAE~
N1*SF*BHSI Insurance Brokers LLC*85*1001234567~
N3*Office 101, Business Centre, DIFC~
N4*Dubai*Dubai*UAE~
N2*BHSI Insurance Brokers LLC~
N3*DIFC~
N4*Dubai*Dubai*AE~
IT1*1*1*C62*50000.00*50000.00***S*5.0~
SAC*C*S****5.0****2500.00~
TDS*2500.00*50000.00*52500.00~
CAD*D*S*5.00*2500.00*50000.00~
CTT*1~
SE*20*000001~
GE*1*1~
IEA*1*000000001~
```

---

## Standard 2: EDIFACT (UN/EDIFACT)

**Scope:** International standard (United Nations)  
**Official Reference:** [UN/EDIFACT — UNECE](https://unece.org/trade/uncefact/)  
**Invoice Format:** Message Type **INVOIC** (Invoice)

### How it works

EDIFACT uses **composite segments** with `:` (segment element separators) and `+` (composite element separators), with `'` as the message terminator.

Example structure for your insurance premium invoice:

```
UNB+UNOC:3+BHSI:1+RECEIVER:1+260723:1030+0001'
UNH+000001+INVOIC:D:96A:UN:AE PINT'
BGM+380+INV-2026-000847+9+20260723'
DTM+137:20260723:102'
DTM+13:20260823:102'
NAD+BY+1009876543::9+Acme Manufacturing FZE'
NAD+SU+1001234567::9+BHSI Insurance Brokers LLC'
TAX+7+S+5.0'
LIN+1++POL-2026-08847'
QTY+47:1'
MOA+203:50000.00'
MOA+204:2500.00'
MOA+150:52500.00'
UNS+S'
MOA+125:50000.00'
MOA+130:2500.00'
MOA+112:52500.00'
UNT+18+000001'
UNZ+1+0001'
```

**Key segments for your invoice:**
| Segment | Purpose | Your Values |
|---------|---------|-------------|
| UNB | Message header | Sender (BHSI), receiver, timestamp |
| UNH | Message begin | Message type (INVOIC), version |
| BGM | Beginning of message | Document type (380), invoice #, date |
| DTM | Date/time | Issue date, due date |
| NAD | Name/address | Buyer (BY), seller (SU) with party IDs |
| RFF | Reference | Optional: transaction reference, etc. |
| CUX | Currency | Currency code (AED) |
| TAX | Tax detail | Tax category (S = standard), rate (5.0) |
| LIN | Line item header | Line sequence, item reference |
| QTY | Quantity | Invoiced quantity (1), UOM (C62) |
| MOA | Monetary amount | Net amount, tax amount, gross amount |
| UNS | Section control | Separator (S = summary section) |
| UNT | Message trailer | Line count, message # |
| UNZ | Segment count | Total messages, release # |

**UAE Insurance Premium Invoice in EDIFACT INVOIC:**

```
UNB+UNOC:3+BHSI:1+RECEIVER:1+260723:1030+0001'
UNH+000001+INVOIC:D:96A:UN'
BGM+380+INV-2026-000847+9+20260723'
DTM+137:20260723:102'
DTM+13:20260823:102'
NAD+BY+1009876543::9+Acme Manufacturing FZE+Unit 12, Jebel Ali Free Zone+Dubai+Dubai+AE'
NAD+SU+1001234567::9+BHSI Insurance Brokers LLC+Office 101, Business Centre, DIFC+Dubai+Dubai+AE'
CUX+2+AED:9'
TAX+7+S+5.0+50000.00+2500.00'
LIN+1++POL-2026-08847'
QTY+47:1:C62'
MOA+203:50000.00'
PIA+1++POL-2026-08847:IN'
IMD+F++:::Annual property and casualty insurance policy for Acme Manufacturing FZE'
TAX+7+S+5.0'
MOA+203:50000.00'
MOA+204:2500.00'
UNS+S'
MOA+125:50000.00'
MOA+130:2500.00'
MOA+112:52500.00'
UNT+21+000001'
UNZ+1+0001'
```

---

## Standard 3: UBL XML (Universal Business Language)

**Scope:** International XML-based standard  
**Official Reference:** [OASIS UBL — GitHub](https://github.com/oasis-tcs/ubl-schema)  
**Invoice Format:** **UBL Invoice** schema

### How it works

UBL is XML-based and human-readable. It's increasingly used alongside or instead of traditional EDI, especially in PEPPOL networks (which is what EDICOM AE PINT is based on).

**UAE Insurance Premium Invoice in UBL XML:**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
         xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
         xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">

  <cbc:UBLVersionID>2.1</cbc:UBLVersionID>
  <cbc:CustomizationID>urn:peppol:pint:billing-1@ae-1</cbc:CustomizationID>
  <cbc:ProfileID>urn:peppol:bis:billing</cbc:ProfileID>

  <cbc:ID>INV-2026-000847</cbc:ID>
  <cbc:IssueDate>2026-07-23</cbc:IssueDate>
  <cbc:DueDate>2026-08-23</cbc:DueDate>
  <cbc:InvoiceTypeCode>380</cbc:InvoiceTypeCode>
  <cbc:DocumentCurrencyCode>AED</cbc:DocumentCurrencyCode>
  <cbc:BuyerReference>POL-2026-08847</cbc:BuyerReference>

  <!-- Seller (BHSI) -->
  <cac:AccountingSupplierParty>
    <cac:Party>
      <cbc:EndpointID schemeID="0235">1001234567</cbc:EndpointID>
      <cac:PartyName>
        <cbc:Name>BHSI Insurance Brokers LLC</cbc:Name>
      </cac:PartyName>
      <cac:PartyLegalEntity>
        <cbc:RegistrationName>BHSI Insurance Brokers LLC</cbc:RegistrationName>
        <cbc:CompanyID schemeID="TL">TL-DXB-456789</cbc:CompanyID>
      </cac:PartyLegalEntity>
      <cac:PartyTaxScheme>
        <cbc:CompanyID>100123456700003</cbc:CompanyID>
        <cac:TaxScheme>
          <cbc:ID>VAT</cbc:ID>
        </cac:TaxScheme>
      </cac:PartyTaxScheme>
      <cac:PostalAddress>
        <cbc:StreetName>Office 101, Business Centre, DIFC</cbc:StreetName>
        <cbc:CityName>Dubai</cbc:CityName>
        <cbc:CountrySubentity>Dubai</cbc:CountrySubentity>
        <cac:Country>
          <cbc:IdentificationCode>AE</cbc:IdentificationCode>
        </cac:Country>
      </cac:PostalAddress>
    </cac:Party>
  </cac:AccountingSupplierParty>

  <!-- Buyer (Acme Manufacturing) -->
  <cac:AccountingCustomerParty>
    <cac:Party>
      <cbc:EndpointID schemeID="0235">1009876543</cbc:EndpointID>
      <cac:PartyName>
        <cbc:Name>Acme Manufacturing FZE</cbc:Name>
      </cac:PartyName>
      <cac:PartyTaxScheme>
        <cbc:CompanyID>100987654300003</cbc:CompanyID>
        <cac:TaxScheme>
          <cbc:ID>VAT</cbc:ID>
        </cac:TaxScheme>
      </cac:PartyTaxScheme>
      <cac:PostalAddress>
        <cbc:StreetName>Unit 12, Jebel Ali Free Zone</cbc:StreetName>
        <cbc:CityName>Dubai</cbc:CityName>
        <cbc:CountrySubentity>Dubai</cbc:CountrySubentity>
        <cac:Country>
          <cbc:IdentificationCode>AE</cbc:IdentificationCode>
        </cac:Country>
      </cac:PostalAddress>
    </cac:Party>
  </cac:AccountingCustomerParty>

  <!-- Invoice Line Item -->
  <cac:InvoiceLine>
    <cbc:ID>1</cbc:ID>
    <cbc:InvoicedQuantity unitCode="C62">1</cbc:InvoicedQuantity>
    <cbc:LineExtensionAmount currencyID="AED">50000.00</cbc:LineExtensionAmount>

    <cac:Item>
      <cbc:Name>POL-2026-08847</cbc:Name>
      <cbc:Description>Annual property and casualty insurance policy for Acme Manufacturing FZE — coverage period 2026-07-23 to 2027-07-22</cbc:Description>
      <cac:ClassifiedTaxCategory>
        <cbc:ID>S</cbc:ID>
        <cbc:Percent>5.0</cbc:Percent>
        <cac:TaxScheme>
          <cbc:ID>VAT</cbc:ID>
        </cac:TaxScheme>
      </cac:ClassifiedTaxCategory>
    </cac:Item>

    <cac:Price>
      <cbc:PriceAmount currencyID="AED">50000.00</cbc:PriceAmount>
      <cbc:BaseQuantity unitCode="C62">1</cbc:BaseQuantity>
    </cac:Price>

    <cac:TaxTotal>
      <cbc:TaxAmount currencyID="AED">2500.00</cbc:TaxAmount>
      <cac:TaxSubtotal>
        <cbc:TaxableAmount currencyID="AED">50000.00</cbc:TaxableAmount>
        <cbc:TaxAmount currencyID="AED">2500.00</cbc:TaxAmount>
        <cac:TaxCategory>
          <cbc:ID>S</cbc:ID>
          <cbc:Percent>5.0</cbc:Percent>
          <cac:TaxScheme>
            <cbc:ID>VAT</cbc:ID>
          </cac:TaxScheme>
        </cac:TaxCategory>
      </cac:TaxSubtotal>
    </cac:TaxTotal>
  </cac:InvoiceLine>

  <!-- Tax Total -->
  <cac:TaxTotal>
    <cbc:TaxAmount currencyID="AED">2500.00</cbc:TaxAmount>
    <cac:TaxSubtotal>
      <cbc:TaxableAmount currencyID="AED">50000.00</cbc:TaxableAmount>
      <cbc:TaxAmount currencyID="AED">2500.00</cbc:TaxAmount>
      <cac:TaxCategory>
        <cbc:ID>S</cbc:ID>
        <cbc:Percent>5.0</cbc:Percent>
        <cac:TaxScheme>
          <cbc:ID>VAT</cbc:ID>
        </cac:TaxScheme>
      </cac:TaxCategory>
    </cac:TaxSubtotal>
  </cac:TaxTotal>

  <!-- Totals -->
  <cac:LegalMonetaryTotal>
    <cbc:LineExtensionTotalAmount currencyID="AED">50000.00</cbc:LineExtensionTotalAmount>
    <cbc:TaxExclusiveAmount currencyID="AED">50000.00</cbc:TaxExclusiveAmount>
    <cbc:TaxInclusiveAmount currencyID="AED">52500.00</cbc:TaxInclusiveAmount>
    <cbc:AllowanceTotalAmount currencyID="AED">0.00</cbc:AllowanceTotalAmount>
    <cbc:PayableAmount currencyID="AED">52500.00</cbc:PayableAmount>
  </cac:LegalMonetaryTotal>

  <!-- Payment Terms -->
  <cac:PaymentTerms>
    <cbc:Note>Payment due by 2026-08-23</cbc:Note>
  </cac:PaymentTerms>

</Invoice>
```

---

## Comparison Table

| Aspect                | X12 810                 | EDIFACT INVOIC          | UBL XML                              |
| --------------------- | ----------------------- | ----------------------- | ------------------------------------ |
| **Origin**            | North America (ANSI)    | UN international        | OASIS international                  |
| **Format**            | Fixed/delimited text    | Delimited text          | XML                                  |
| **Readability**       | Low (cryptic)           | Low (cryptic)           | High (self-describing)               |
| **Adoption**          | US/Canada/Mexico        | Europe/International    | Growing (PEPPOL, newer systems)      |
| **File Size**         | Small                   | Small                   | Medium                               |
| **Parsing**           | Specialized EDI parsers | Specialized EDI parsers | XML libraries (ubiquitous)           |
| **For UAE e-Invoice** | Not typical             | Not typical             | **Preferred (PEPPOL AE PINT basis)** |
| **EDICOM Support**    | Likely                  | Likely                  | **Most likely (PEPPOL profile)**     |

---

## EDICOM iPaaS API Reference

Below are the endpoints used at each step of the pipeline. All endpoints require an OAuth 2.0 bearer token (acquired via the token endpoint). Reference: [EDICOM iPaaS Docs](https://ipaas-docs.edicomgroup.com/docs/openapi/eipaas-server-api/)

### Step 1: Authenticate

**Endpoint:** `POST https://accounts.edicomgroup.com/token`

**Purpose:** Acquire an OAuth 2.0 access token for subsequent API calls.

**Request:**

```
POST /token HTTP/1.1
Host: accounts.edicomgroup.com
Content-Type: application/x-www-form-urlencoded

grant_type=password
&username={username}
&password={password}
&scope=openid
```

**Response:**

```json
{
  "access_token": "eyJ0eXAiOiJKV1QiLCJhbGc...",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

**Processor action:** Cache the token and refresh proactively ~30 seconds before expiry. Do not request a new token per invoice.

---

### Step 2: Submit Invoice

**Endpoint:** `POST /publish`

**Purpose:** Submit a single invoice document to EDICOM for processing.

**Request headers:**

```
Authorization: Bearer {access_token}
Content-Type: application/json
```

**Request body** (inferred from integration pattern):

```json
{
  "document": {
    "invoiceNumber": "INV-12345",
    "invoiceDate": "2026-07-22",
    "amount": 1500.00,
    "currency": "USD",
    "... other invoice fields ..."
  }
}
```

**Response** (expected):

```json
{
  "transactionId": "TXN-abc123def456",
  "status": "submitted",
  "timestamp": "2026-07-22T10:30:00Z"
}
```

**Processor action:** Extract `transactionId`, update `invint.InvoiceIntegration` with `Status = WaitingConfirmation`.

> **Note:** EDICOM does not expose a batch endpoint. Submit invoices individually or concurrently under a single token (see Options 3/4/5b for concurrent patterns).

---

### Step 3: Receive Status — Webhook (Option 1)

**Endpoint:** `POST {your-webhook-url}` (inbound from EDICOM)

**Purpose:** EDICOM pushes the final status of an invoice when processing completes.

**Inbound request body** (expected):

```json
{
  "transactionId": "TXN-abc123def456",
  "invoiceNumber": "INV-12345",
  "status": "acknowledged|rejected|failed",
  "edicomReference": "REF-xyz789",
  "errorCode": "ERR_001",
  "errorMessage": "Invalid amount"
}
```

**Webhook handler action:**

1. Validate the request is from EDICOM (HMAC signature, IP allowlist, or bearer token).
2. Query `invint.InvoiceIntegration` for the `transactionId`.
3. Update `Status = Acknowledged` or `Failed`, record `EdicomReference` if present.
4. Return HTTP 200 to confirm delivery.

> **Requirements:** Publicly reachable HTTPS endpoint. Idempotent — duplicate callbacks for the same transactionId must not create duplicate records.

---

### Step 4: Poll Status — Reconciler (All Options)

**Endpoint:** `GET /messages`

**Purpose:** Query the status of submitted invoices. Acts as the primary mechanism for polling-only options (2, 4) or fallback safety net for webhook options (1, 3, 5).

**Request:**

```
GET /messages?transactionId={transactionId}&status=*
Authorization: Bearer {access_token}
```

**Response** (expected):

```json
{
  "messages": [
    {
      "transactionId": "TXN-abc123def456",
      "documentId": "DOC-12345",
      "status": "acknowledged|pending|failed",
      "edicomReference": "REF-xyz789",
      "errorCode": "ERR_001",
      "errorMessage": "Invalid amount",
      "processedAt": "2026-07-22T10:35:00Z"
    }
  ]
}
```

**Reconciler action:**

1. Query `invint.InvoiceIntegration` for `Status = WaitingConfirmation AND SubmittedAt < NOW() - @GraceMinutes`.
2. For each `transactionId`, call `GET /messages`.
3. If status is `acknowledged` → update the row to `Acknowledged`.
4. If status is `failed` → update the row to `Failed` with error code/message.
5. If still `pending` → increment `ReconcileAttempts`; if >= max, write `Failed` + alert.

**Polling interval:** 5–15 minutes (configurable per option).

---

---

## References

- **X12 Standards:** [ASC X12 — https://www.x12.org/](https://www.x12.org/)
- **EDIFACT:** [UN/EDIFACT — https://unece.org/trade/uncefact/](https://unece.org/trade/uncefact/)
- **UBL XML:** [OASIS UBL — https://github.com/oasis-tcs/ubl-schema](https://github.com/oasis-tcs/ubl-schema)
- **PEPPOL AE PINT:** [PEPPOL Authority — https://peppol.eu/](https://peppol.eu/)
- **UAE e-Invoice Specs:** [ZATCA (Saudi) and UAE FTA guidance](https://www.fta.gov.ae/en/business-services/e-invoicing)
