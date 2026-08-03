CREATE TABLE IF NOT EXISTS UserLoginHistory
(
    ID BIGINT NOT NULL AUTO_INCREMENT,
    UserCode VARCHAR(50) NOT NULL,
    UserName VARCHAR(100) NOT NULL,
    LoginLocation VARCHAR(100),
    LoginDateTime DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LogoutDateTime DATETIME NULL,
    IPAddress VARCHAR(45),
    AuditTrail TEXT,
    PRIMARY KEY (ID),
    INDEX IX_UserLoginHistory_UserCode_LoginDateTime (UserCode, LoginDateTime),
    INDEX IX_UserLoginHistory_LoginDateTime (LoginDateTime)
);

SET @LogoutDateTimeColumnExists :=
(
    SELECT COUNT(1)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'UserLoginHistory'
      AND COLUMN_NAME = 'LogoutDateTime'
);

SET @LogoutDateTimeSql := IF(
    @LogoutDateTimeColumnExists = 0,
    'ALTER TABLE UserLoginHistory ADD COLUMN LogoutDateTime DATETIME NULL AFTER LoginDateTime',
    'SELECT 1'
);

PREPARE LogoutDateTimeStatement FROM @LogoutDateTimeSql;
EXECUTE LogoutDateTimeStatement;
DEALLOCATE PREPARE LogoutDateTimeStatement;

DROP PROCEDURE IF EXISTS WebApp_IU_UserLoginHistory;
DROP PROCEDURE IF EXISTS WebApp_U_UserLoginHistoryLogout;

DELIMITER $$

CREATE PROCEDURE WebApp_IU_UserLoginHistory
(
    IN P_UserCode VARCHAR(50),
    IN P_UserName VARCHAR(100),
    IN P_LoginLocation VARCHAR(100),
    IN P_IPAddress VARCHAR(45),
    IN P_AuditTrail TEXT
)
BEGIN
    INSERT INTO UserLoginHistory
    (
        UserCode,
        UserName,
        LoginLocation,
        LoginDateTime,
        LogoutDateTime,
        IPAddress,
        AuditTrail
    )
    VALUES
    (
        P_UserCode,
        P_UserName,
        NULLIF(P_LoginLocation, ''),
        NOW(),
        NULL,
        NULLIF(P_IPAddress, ''),
        P_AuditTrail
    );

    SELECT LAST_INSERT_ID() AS UserLoginHistoryID;
END$$

CREATE PROCEDURE WebApp_U_UserLoginHistoryLogout
(
    IN P_UserLoginHistoryID BIGINT,
    IN P_UserCode VARCHAR(50)
)
BEGIN
    DECLARE V_AffectedRows INT DEFAULT 0;

    IF P_UserLoginHistoryID IS NOT NULL AND P_UserLoginHistoryID > 0 THEN
        UPDATE UserLoginHistory
        SET LogoutDateTime = NOW()
        WHERE ID = P_UserLoginHistoryID
          AND LogoutDateTime IS NULL;

        SET V_AffectedRows = ROW_COUNT();
    END IF;

    IF V_AffectedRows = 0 AND P_UserCode IS NOT NULL AND P_UserCode <> '' THEN
        UPDATE UserLoginHistory
        SET LogoutDateTime = NOW()
        WHERE UserCode = P_UserCode
          AND LogoutDateTime IS NULL
        ORDER BY LoginDateTime DESC, ID DESC
        LIMIT 1;
    END IF;
END$$

DELIMITER ;