-- OCIMS schema. Also created automatically at app startup by
-- DatabaseHelper.EnsureSchema(); kept here as reference documentation.
-- All statements are idempotent (IF NOT EXISTS / seed-only-when-empty).
--
-- NOTE: the OCIMS fund table is named `ocims_fund_sources` because the
-- shared CRS database already has an unrelated `fund_sources` table.

CREATE TABLE IF NOT EXISTS departments (
    dept_id   INT AUTO_INCREMENT PRIMARY KEY,
    dept_name VARCHAR(100) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS employees (
    emp_id            INT AUTO_INCREMENT PRIMARY KEY,
    employee_no       VARCHAR(30)  NOT NULL UNIQUE,
    first_name        VARCHAR(80)  NOT NULL,
    middle_name       VARCHAR(80)  NULL,
    last_name         VARCHAR(80)  NOT NULL,
    suffix            VARCHAR(20)  NULL,
    gender            VARCHAR(20)  NULL,
    civil_status      VARCHAR(20)  NULL,
    email             VARCHAR(150) NULL,
    phone_mobile      VARCHAR(30)  NULL,
    phone_office      VARCHAR(30)  NULL,
    address_line1     VARCHAR(255) NULL,
    date_of_birth     DATE         NULL,
    date_hired        DATE         NULL,
    position_title    VARCHAR(100) NULL,
    employment_type   VARCHAR(40)  NULL,
    employment_status VARCHAR(20)  NOT NULL DEFAULT 'Active',
    dept_id           INT          NULL,
    created_at        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_emp_dept FOREIGN KEY (dept_id) REFERENCES departments(dept_id)
);

CREATE TABLE IF NOT EXISTS system_users (
    user_id       INT AUTO_INCREMENT PRIMARY KEY,
    username      VARCHAR(50)  NOT NULL UNIQUE,
    password_hash VARCHAR(100) NOT NULL,
    role          VARCHAR(40)  NOT NULL DEFAULT 'Employee',
    emp_id        INT          NULL,
    is_active     TINYINT(1)   NOT NULL DEFAULT 1,
    last_login    DATETIME     NULL,
    created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_user_emp FOREIGN KEY (emp_id) REFERENCES employees(emp_id)
);

CREATE TABLE IF NOT EXISTS insurance_policies (
    policy_id       INT AUTO_INCREMENT PRIMARY KEY,
    policy_code     VARCHAR(30)   NOT NULL UNIQUE,
    policy_name     VARCHAR(150)  NOT NULL,
    policy_type     VARCHAR(60)   NULL,
    provider_name   VARCHAR(150)  NULL,
    coverage_amount DECIMAL(14,2) NOT NULL DEFAULT 0,
    effective_date  DATE          NULL,
    expiry_date     DATE          NULL,
    policy_status   VARCHAR(20)   NOT NULL DEFAULT 'Active',
    description     TEXT          NULL
);

CREATE TABLE IF NOT EXISTS claims (
    claim_id             INT AUTO_INCREMENT PRIMARY KEY,
    claim_no             VARCHAR(30)   NOT NULL UNIQUE,
    emp_id               INT           NOT NULL,
    policy_id            INT           NULL,
    claim_type           VARCHAR(60)   NULL,
    claim_date           DATE          NULL,
    amount_claimed       DECIMAL(12,2) NOT NULL DEFAULT 0,
    claim_status         VARCHAR(20)   NOT NULL DEFAULT 'Pending',
    incident_description TEXT          NULL,
    submitted_date       DATETIME      NULL,
    CONSTRAINT fk_claim_emp    FOREIGN KEY (emp_id)    REFERENCES employees(emp_id),
    CONSTRAINT fk_claim_policy FOREIGN KEY (policy_id) REFERENCES insurance_policies(policy_id)
);

CREATE TABLE IF NOT EXISTS document_types (
    doc_type_id INT AUTO_INCREMENT PRIMARY KEY,
    type_name   VARCHAR(80) NOT NULL UNIQUE,
    description VARCHAR(255) NULL,
    is_active   TINYINT(1)  NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS documents (
    document_id INT AUTO_INCREMENT PRIMARY KEY,
    emp_id      INT          NOT NULL,
    doc_type_id INT          NOT NULL,
    doc_title   VARCHAR(200) NOT NULL,
    file_name   VARCHAR(255) NULL,
    file_path   VARCHAR(500) NULL,
    file_size   VARCHAR(30)  NULL,
    remarks     TEXT         NULL,
    is_active   TINYINT(1)   NOT NULL DEFAULT 1,
    created_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_doc_emp  FOREIGN KEY (emp_id)      REFERENCES employees(emp_id),
    CONSTRAINT fk_doc_type FOREIGN KEY (doc_type_id) REFERENCES document_types(doc_type_id)
);

CREATE TABLE IF NOT EXISTS senders (
    sender_id   INT AUTO_INCREMENT PRIMARY KEY,
    sender_name VARCHAR(150) NOT NULL,
    sender_type VARCHAR(60)  NULL,
    email       VARCHAR(150) NULL,
    phone       VARCHAR(30)  NULL,
    address     VARCHAR(255) NULL,
    is_active   TINYINT(1)   NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS receivers (
    receiver_id   INT AUTO_INCREMENT PRIMARY KEY,
    receiver_name VARCHAR(150) NOT NULL,
    receiver_type VARCHAR(60)  NULL,
    department    VARCHAR(100) NULL,
    email         VARCHAR(150) NULL,
    phone         VARCHAR(30)  NULL,
    address       VARCHAR(255) NULL,
    is_active     TINYINT(1)   NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS document_transactions (
    transaction_id   INT AUTO_INCREMENT PRIMARY KEY,
    transaction_no   VARCHAR(30)  NOT NULL UNIQUE,
    transaction_type VARCHAR(40)  NOT NULL,
    sender_id        INT          NULL,
    receiver_id      INT          NULL,
    document_id      INT          NULL,
    subject          VARCHAR(200) NOT NULL,
    description      TEXT         NULL,
    transaction_date DATE         NULL,
    due_date         DATE         NULL,
    priority         VARCHAR(20)  NOT NULL DEFAULT 'Normal',
    status           VARCHAR(20)  NOT NULL DEFAULT 'Pending',
    remarks          TEXT         NULL,
    created_at       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_tx_sender   FOREIGN KEY (sender_id)   REFERENCES senders(sender_id),
    CONSTRAINT fk_tx_receiver FOREIGN KEY (receiver_id) REFERENCES receivers(receiver_id),
    CONSTRAINT fk_tx_document FOREIGN KEY (document_id) REFERENCES documents(document_id)
);

CREATE TABLE IF NOT EXISTS payments (
    payment_id     INT AUTO_INCREMENT PRIMARY KEY,
    payment_no     VARCHAR(30)  NOT NULL UNIQUE,
    emp_id         INT          NULL,
    client_name    VARCHAR(150) NOT NULL,
    amount         DECIMAL(12,2) NOT NULL,
    payment_date   DATE         NOT NULL,
    payment_mode   VARCHAR(40)  NULL,
    payment_status VARCHAR(20)  NOT NULL DEFAULT 'Paid',
    notes          TEXT         NULL,
    created_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_pay_emp FOREIGN KEY (emp_id) REFERENCES employees(emp_id)
);

CREATE TABLE IF NOT EXISTS ocims_fund_sources (
    fund_id       INT AUTO_INCREMENT PRIMARY KEY,
    fund_name     VARCHAR(150) NOT NULL,
    fund_type     VARCHAR(40)  NULL,
    amount        DECIMAL(14,2) NOT NULL,
    source        VARCHAR(150) NULL,
    date_received DATE         NULL,
    fund_status   VARCHAR(20)  NOT NULL DEFAULT 'Active',
    notes         TEXT         NULL,
    created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO departments (dept_name)
SELECT 'General' WHERE NOT EXISTS (SELECT 1 FROM departments);

INSERT INTO document_types (type_name)
SELECT * FROM (SELECT 'Valid ID' UNION SELECT 'Policy Document'
               UNION SELECT 'Claim Form' UNION SELECT 'Medical Certificate'
               UNION SELECT 'Proof of Payment' UNION SELECT 'Other') AS seed
WHERE NOT EXISTS (SELECT 1 FROM document_types);
