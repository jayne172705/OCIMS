CREATE TABLE IF NOT EXISTS `user_permissions` (
    `permission_id` INT NOT NULL AUTO_INCREMENT,
    `user_id` INT NOT NULL,
    `can_access_dashboard` TINYINT(1) NOT NULL DEFAULT 1,
    `can_access_employees` TINYINT(1) NOT NULL DEFAULT 1,
    `can_access_beneficiaries` TINYINT(1) NOT NULL DEFAULT 1,
    `can_access_policies` TINYINT(1) NOT NULL DEFAULT 1,
    `can_access_claims` TINYINT(1) NOT NULL DEFAULT 1,
    `can_access_premiums` TINYINT(1) NOT NULL DEFAULT 1,
    `can_access_benefits` TINYINT(1) NOT NULL DEFAULT 1,
    `can_access_documents` TINYINT(1) NOT NULL DEFAULT 1,
    `can_access_transactions` TINYINT(1) NOT NULL DEFAULT 1,
    `can_access_cedulas` TINYINT(1) NOT NULL DEFAULT 1,
    `can_access_reports` TINYINT(1) NOT NULL DEFAULT 1,
    `can_access_company_profile` TINYINT(1) NOT NULL DEFAULT 1,
    `can_access_settings` TINYINT(1) NOT NULL DEFAULT 1,
    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`permission_id`),
    UNIQUE KEY `uq_user_permissions_user_id` (`user_id`),
    CONSTRAINT `fk_user_permissions_system_users`
        FOREIGN KEY (`user_id`) REFERENCES `system_users` (`user_id`)
        ON DELETE CASCADE
);
