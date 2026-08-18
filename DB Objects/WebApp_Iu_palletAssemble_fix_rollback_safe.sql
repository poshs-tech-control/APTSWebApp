DROP PROCEDURE IF EXISTS `WebApp_Iu_palletAssemble`;

DELIMITER $$

CREATE DEFINER=`Cinoti`@`%` PROCEDURE `WebApp_Iu_palletAssemble`(
    IN P_PalletCode VARCHAR(20),
    IN P_TotalMember VARCHAR(20),
    IN P_ClientLocation VARCHAR(200),
    IN P_Active VARCHAR(5),
    IN P_CreatedBy VARCHAR(50),
    IN P_IPAdd VARCHAR(200),
    IN P_PalletOrderCode VARCHAR(200),
    IN P_PalletOrderdtlCode VARCHAR(200),
    IN P_SheetWidth VARCHAR(50),
    IN P_SheetLength1 VARCHAR(50),
    IN P_SheetLength2 VARCHAR(50)
)
BEGIN
    DECLARE P_CodeInt BIGINT DEFAULT 0;

    IF P_PalletCode = '0' THEN
        INSERT INTO palletconfiguration
        (
            PalletCode,
            TotalMember,
            PalletCreatedDate,
            PalletCreatedBy,
            Wooden,
            ClientLocation,
            Active,
            CreatedBy,
            CreatedDate,
            CreatedIP,
            ModifiedBy,
            ModifiedDate,
            ModifiedIP,
            PalletOrderCode,
            SheetWidth,
            SheetLength1,
            SheetLength2
        )
        VALUES
        (
            '0',
            P_TotalMember,
            NOW(),
            P_CreatedBy,
            'False',
            P_ClientLocation,
            P_Active,
            P_CreatedBy,
            NOW(),
            P_IPAdd,
            P_CreatedBy,
            NOW(),
            P_IPAdd,
            P_PalletOrderCode,
            P_SheetWidth,
            P_SheetLength1,
            P_SheetLength2
        );

        SET P_CodeInt = LAST_INSERT_ID();

        UPDATE palletconfiguration
        SET PalletCode = CONCAT('PLCF', LPAD(P_CodeInt, 8, '0'))
        WHERE TransactionID = P_CodeInt;

        -- Return the pallet created by this call only. Do not use ORDER BY DESC because
        -- simultaneous users can otherwise receive another user's latest pallet code.
        SELECT PalletCode
        FROM palletconfiguration
        WHERE TransactionID = P_CodeInt;

        -- AssembleCounts is intentionally not updated in this master procedure.
        -- The application increments PalletOrderDtl.AssembleCounts only after
        -- WebApp_Bulk_PalletAssembleDt succeeds, inside the same transaction.
        -- Old unsafe logic kept for reference:
        --
        -- SELECT a.AssembleCounts INTO AssembleCounts_new
        -- FROM palletorderdtl a
        -- WHERE a.PalletOrderdtlCode = P_PalletOrderdtlCode;
        --
        -- UPDATE palletorderdtl
        -- SET AssembleCounts = AssembleCounts_new + 1
        -- WHERE PalletOrderdtlCode = P_PalletOrderdtlCode;
    END IF;
END$$

DELIMITER ;