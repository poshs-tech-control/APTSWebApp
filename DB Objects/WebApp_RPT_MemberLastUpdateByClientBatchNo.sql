-- Deploy the updated procedure definition in the application database.
DELIMITER $$
DROP PROCEDURE IF EXISTS `WebApp_RPT_MemberLastUpdateByClientBatchNo`$$
CREATE PROCEDURE `WebApp_RPT_MemberLastUpdateByClientBatchNo`(
    IN p_ClientBatchNo VARCHAR(50)
)
BEGIN
    SELECT ML.RFID, ML.LastEntryBy, ML.LastUpdatedDate, ML.LastTransferTo, ML.LastStatus
    FROM memberlastupdate AS ML
    INNER JOIN palletconfigdt AS PCD ON PCD.RFIDNo = ML.RFID
    INNER JOIN palletpre_dispatch AS PPD ON PPD.PalletCode = PCD.PalletCode
    WHERE PPD.ClientBatchNo = p_ClientBatchNo;
END$$
DELIMITER ;
