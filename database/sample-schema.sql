-- GridLog Sample Schema
-- This is a representative excerpt of the production schema.
-- Production data is not included.

CREATE DATABASE IF NOT EXISTS gridlog CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE gridlog;

-- ── Substations ──────────────────────────────────────────────────────────────
CREATE TABLE substation (
    substation_id   INT AUTO_INCREMENT PRIMARY KEY,
    substation_name VARCHAR(100) NOT NULL,
    location        VARCHAR(200)
);

-- ── Bays / Feeders ───────────────────────────────────────────────────────────
CREATE TABLE bay (
    bay_id          INT AUTO_INCREMENT PRIMARY KEY,
    substation_id   INT NOT NULL,
    bay_name        VARCHAR(100) NOT NULL,
    voltage_level   VARCHAR(20),
    bay_type        VARCHAR(50),    -- 'feeder', 'transformer', 'bus', etc.
    FOREIGN KEY (substation_id) REFERENCES substation(substation_id)
);

-- ── Relay Types ───────────────────────────────────────────────────────────────
CREATE TABLE relay_type (
    relay_type_id   INT AUTO_INCREMENT PRIMARY KEY,
    relay_name      VARCHAR(100) NOT NULL,
    frame_key       VARCHAR(50),    -- 'schneider', 'multilin', 'electromech', 'default'
    phase_notation  VARCHAR(10) DEFAULT 'ABC',
    current_unit    VARCHAR(5)  DEFAULT 'A'
);

-- ── Tripping Records ─────────────────────────────────────────────────────────
CREATE TABLE tripping_records (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    bay_id          INT NOT NULL,
    relay_type_id   INT,
    off_datetime    DATETIME NOT NULL,      -- breaker opened (Nepal local, no offset)
    on_datetime     DATETIME,               -- breaker restored; NULL = still open
    total_time      INT,                    -- minutes (computed on restoration)
    fault_type      VARCHAR(20) DEFAULT 'TEMPORARY',  -- TEMPORARY / PERMANENT / FORCED
    phase_a         TINYINT(1) DEFAULT 0,
    phase_b         TINYINT(1) DEFAULT 0,
    phase_c         TINYINT(1) DEFAULT 0,
    phase_g         TINYINT(1) DEFAULT 0,
    three_phase     TINYINT(1) DEFAULT 0,
    I               DOUBLE,
    Ia              DOUBLE,
    Ib              DOUBLE,
    Ic              DOUBLE,
    Ig              DOUBLE,
    remarks         TEXT,
    FOREIGN KEY (bay_id)        REFERENCES bay(bay_id),
    FOREIGN KEY (relay_type_id) REFERENCES relay_type(relay_type_id)
);

-- ── Protection Elements (relay function codes) ───────────────────────────────
CREATE TABLE protection_element (
    element_id      INT AUTO_INCREMENT PRIMARY KEY,
    element_code    VARCHAR(20) NOT NULL    -- e.g. 'E/F', 'O/C', '21', '87T'
);

CREATE TABLE tripping_elements (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    tripping_id     INT NOT NULL,
    element_id      INT NOT NULL,
    stage           VARCHAR(10),            -- '1', '2', 'Inst', etc.
    FOREIGN KEY (tripping_id) REFERENCES tripping_records(id) ON DELETE CASCADE,
    FOREIGN KEY (element_id)  REFERENCES protection_element(element_id)
);

-- ── Operators ────────────────────────────────────────────────────────────────
CREATE TABLE operators (
    operator_id     INT AUTO_INCREMENT PRIMARY KEY,
    substation_id   INT NOT NULL,
    operator_name   VARCHAR(100) NOT NULL,
    pin_hash        VARCHAR(255),           -- bcrypt hash
    FOREIGN KEY (substation_id) REFERENCES substation(substation_id)
);

-- ── Shutdown Records ─────────────────────────────────────────────────────────
CREATE TABLE shutdown_records (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    bay_id          INT NOT NULL,
    shutdown_type   VARCHAR(20) DEFAULT 'PLANNED',  -- PLANNED / EMERGENCY
    off_datetime    DATETIME NOT NULL,
    on_datetime     DATETIME,
    total_time      INT,
    operator_id     INT,
    closed_by       INT,
    remarks         TEXT,
    FOREIGN KEY (bay_id)       REFERENCES bay(bay_id),
    FOREIGN KEY (operator_id)  REFERENCES operators(operator_id),
    FOREIGN KEY (closed_by)    REFERENCES operators(operator_id)
);

-- ── Sample Data ──────────────────────────────────────────────────────────────
INSERT INTO substation (substation_name, location) VALUES
    ('Patan', 'Lalitpur'),
    ('Khimti', 'Ramechhap');

INSERT INTO bay (substation_id, bay_name, voltage_level, bay_type) VALUES
    (1, 'Baneshwor (66kV)', '66kV', 'feeder'),
    (1, 'Suichatar-1 (11kV)', '11kV', 'feeder'),
    (2, 'Khimti Main (132kV)', '132kV', 'feeder');

INSERT INTO relay_type (relay_name, frame_key, phase_notation, current_unit) VALUES
    ('Schneider P3U30', 'schneider', 'RST', 'A'),
    ('GE Multilin F650', 'multilin', 'ABC', 'kA'),
    ('Electromechanical', 'electromech', 'ABC', 'A');
