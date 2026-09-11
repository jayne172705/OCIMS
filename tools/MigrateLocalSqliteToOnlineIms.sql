-- Adds the operational tables required by the current eSureHi build to an
-- existing IMS MySQL database. This script does not delete or modify rows in
-- existing tables.

CREATE TABLE IF NOT EXISTS distribution_batches (
    batch_id INT NOT NULL AUTO_INCREMENT,
    project_code LONGTEXT NOT NULL,
    project_title LONGTEXT NOT NULL,
    project_description LONGTEXT NULL,
    source_fund_id INT NULL,
    amount_per_beneficiary DECIMAL(65,30) NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    SyncId VARCHAR(36) NOT NULL,
    CONSTRAINT PK_distribution_batches PRIMARY KEY (batch_id),
    CONSTRAINT UX_distribution_batches_SyncId UNIQUE (SyncId),
    INDEX IX_distribution_batches_source_fund_id (source_fund_id)
);

CREATE TABLE IF NOT EXISTS distribution_records (
    record_id INT NOT NULL AUTO_INCREMENT,
    batch_id INT NOT NULL,
    beneficiary_id INT NOT NULL,
    status LONGTEXT NOT NULL,
    remarks LONGTEXT NULL,
    processed_at DATETIME(6) NULL,
    fund_debited TINYINT(1) NOT NULL DEFAULT 0,
    SyncId VARCHAR(36) NOT NULL,
    CONSTRAINT PK_distribution_records PRIMARY KEY (record_id),
    CONSTRAINT UX_distribution_records_SyncId UNIQUE (SyncId),
    INDEX IX_distribution_records_batch_id (batch_id),
    INDEX IX_distribution_records_beneficiary_id (beneficiary_id)
);

CREATE TABLE IF NOT EXISTS payments (
    payment_id INT NOT NULL AUTO_INCREMENT,
    beneficiary_id INT NOT NULL,
    family_id LONGTEXT NULL,
    member_name LONGTEXT NOT NULL,
    dependent_name LONGTEXT NULL,
    relationship LONGTEXT NULL,
    billing_month DATE NOT NULL,
    amount DECIMAL(65,30) NOT NULL,
    status LONGTEXT NOT NULL DEFAULT 'Pending',
    payment_type LONGTEXT NOT NULL DEFAULT 'Advance',
    source_of_funds LONGTEXT NULL,
    paid_at DATE NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    created_by LONGTEXT NULL,
    remarks LONGTEXT NULL,
    CONSTRAINT PK_payments PRIMARY KEY (payment_id),
    INDEX IX_payments_beneficiary_id (beneficiary_id),
    INDEX IX_payments_family_id (family_id(191))
);
