DROP PROCEDURE IF EXISTS `WebApp_Get_CollectionDetails`;

DELIMITER $$

CREATE DEFINER=`Cinoti`@`%` PROCEDURE `WebApp_Get_CollectionDetails`(
 IN P_CollectionCode VARCHAR(20)
 )
BEGIN
 
 SET SESSION sql_mode=(SELECT REPLACE(@@sql_mode,'ONLY_FULL_GROUP_BY',''));
 
 SELECT A.CollectionCode, 
        A.FromLocationType AS FromLocationTypeCode,
        A.FromLocation AS FromLocationCode,
        FT.ParameterName AS FromLocationType,
        FL.LocationTitle AS FromLocation, 
        TT.ParameterName AS ToLocationType,
        TL.LocationTitle AS ToLocation, 
        A.ToLocationType AS ToLocationTypeCode,
        A.ToLocation AS ToLocationCode, 
        A.CustomerCode, 
        A.TruckNo, 
        A.DriverName, 
        A.DriverNo, 
        A.CreatedBy, 
        C.CustName,
        A.GenerateChallan,
        U.UserMailId As UserName,
        A.TruckNo As VehicleInToLoc,
        YA.VehicleNo As VehicleInFromLoc,
        A.TransportCode As Transporter,
        Z.ShipToLocTitle As CustsomerInFromLoc,
        Z.EmailId as CustsomerInFromLocMailId,
        ZA.ShipToLocTitle As CustsomerInToLoc
 FROM membercollection AS A
 LEFT JOIN miscparameter AS FT ON A.FromLocationType = FT.ParameterCode
 LEFT JOIN miscparameter AS TT ON A.ToLocationType = TT.ParameterCode
 LEFT JOIN customermaster AS C ON A.CustomerCode = C.CustomerCode
 LEFT JOIN locationmaster AS FL ON A.FromLocation = FL.LocationCode
 LEFT JOIN locationmaster AS TL ON A.ToLocation = TL.LocationCode
 LEFT JOIN usermaster AS U ON A.CreatedBy = U.UserCode
 LEFT JOIN transportmaster As Y ON A.ToLocation = Y.TransportCode
 LEFT JOIN transportmaster As YA ON A.FromLocation = YA.TransportCode
 LEFT JOIN shiptolocation As Z ON A.FromLocation = Z.ShipToLocCode
 LEFT JOIN shiptolocation As ZA ON A.ToLocation = ZA.ShipToLocCode
 WHERE A.ACTIVE = 'TRUE'
 AND ((P_CollectionCode IS NULL) OR (A.CollectionCode = P_CollectionCode));
 
 SELECT ClientBatchNo,
        GROUP_CONCAT(MemberSize ORDER BY SizeDisplayOrder, MemberSize SEPARATOR 'X') As PalletMemberSize,
        GROUP_CONCAT(size_count ORDER BY SizeDisplayOrder, MemberSize SEPARATOR 'X') AS PalletMemberQty,
        DispatchShipToCode,
        DispatchedShipToName
 FROM (
       SELECT COUNT(D.RFID) AS size_count,
              A.MemberSize As MemberSize,
              E.CollectionCode As CollectionCode,
              D.CustDismentleCode As CustDismentleCode,
              G.ShipToLocation As DispatchShipToCode,
              H.ShipToLocTitle As DispatchedShipToName,
              I.ClientBatchNo As ClientBatchNo,
              CASE
                  WHEN UPPER(COALESCE(C.ParameterName, '')) LIKE 'CROSS%' THEN 1
                  WHEN UPPER(COALESCE(C.ParameterName, '')) LIKE 'CM%' THEN 1
                  WHEN UPPER(COALESCE(C.ParameterName, '')) LIKE 'LONG%' THEN 2
                  WHEN UPPER(COALESCE(C.ParameterName, '')) LIKE 'LM%' THEN 2
                  ELSE 9
              END AS SizeDisplayOrder
       FROM skewmaster AS A  
       LEFT JOIN membermaster B ON B.SKEWCode = A.SKEWCode
       LEFT JOIN miscparameter C ON C.ParameterCode = B.MemberType
       LEFT JOIN membercollectiondtl D ON D.MemberCode = B.MemberCode
       LEFT JOIN membercollection E ON E.CollectionCode = D.CollectionCode
       LEFT JOIN palletdispatcheddtl F ON F.MemberCode = B.MemberCode
       LEFT JOIN palletdispatched G ON G.PalletDispCode = F.PalletDispCode
       LEFT JOIN shiptolocation AS H ON H.ShipToLocCode = G.ShipToLocation
       LEFT JOIN palletdismentlecustomerdt I ON I.MemberCode = B.MemberCode
       WHERE D.ACTIVE = 'False'
       AND E.CollectionCode = P_CollectionCode
       AND I.ActionType = 'Y'
       AND I.Active = 'True'
       AND F.Active = 'True'
       GROUP BY A.MemberSize, SizeDisplayOrder, E.CollectionCode, D.CustDismentleCode, I.ClientBatchNo
 ) AS subquery
 GROUP BY CustDismentleCode, ClientBatchNo;
 
 SELECT MemberSize As MemberSKU,size_count As Qty
 FROM (
       SELECT COUNT(D.RFID) AS size_count,
              CONCAT(C.ParameterName,' ',A.MemberSize) As MemberSize,
              E.CollectionCode As CollectionCode,
              D.CustDismentleCode As CustDismentleCode,
              G.ShipToLocation As DispatchShipToCode,
              H.ShipToLocTitle As DispatchedShipToName,
              I.ClientBatchNo As ClientBatchNo
       FROM skewmaster AS A  
       LEFT JOIN membermaster B ON B.SKEWCode = A.SKEWCode
       LEFT JOIN miscparameter C ON C.ParameterCode = B.MemberType
       LEFT JOIN membercollectiondtl D ON D.MemberCode = B.MemberCode
       LEFT JOIN membercollection E ON E.CollectionCode = D.CollectionCode
       LEFT JOIN palletdispatcheddtl F ON F.MemberCode = B.MemberCode
       LEFT JOIN palletdispatched G ON G.PalletDispCode = F.PalletDispCode
       LEFT JOIN shiptolocation AS H ON H.ShipToLocCode = G.ShipToLocation
       LEFT JOIN palletdismentlecustomerdt I ON I.MemberCode = B.MemberCode
       WHERE D.ACTIVE = 'True'
       AND E.CollectionCode = P_CollectionCode
       AND I.Active = 'True'
       AND F.Active = 'True'
       GROUP BY A.MemberSize, C.ParameterName, E.CollectionCode
 ) AS subquery
 GROUP BY CustDismentleCode, MemberSKU;
 
 SELECT ClientBatchNo,
        GROUP_CONCAT(MemberSize ORDER BY SizeDisplayOrder, MemberSize SEPARATOR 'X') As PalletMemberSize,
        GROUP_CONCAT(size_count ORDER BY SizeDisplayOrder, MemberSize SEPARATOR 'X') AS PalletMemberQty,
        DispatchedShipToName,
        ActionType
 FROM (
       SELECT COUNT(D.RFIDNo) AS size_count,
              A.MemberSize As MemberSize,
              D.ClientBatchNo As ClientBatchNo,
              D.ActionType As ActionType,
              G.ShipToLocation As DispatchShipToCode,
              H.ShipToLocTitle As DispatchedShipToName,
              CASE
                  WHEN UPPER(COALESCE(C.ParameterName, '')) LIKE 'CROSS%' THEN 1
                  WHEN UPPER(COALESCE(C.ParameterName, '')) LIKE 'CM%' THEN 1
                  WHEN UPPER(COALESCE(C.ParameterName, '')) LIKE 'LONG%' THEN 2
                  WHEN UPPER(COALESCE(C.ParameterName, '')) LIKE 'LM%' THEN 2
                  ELSE 9
              END AS SizeDisplayOrder
       FROM skewmaster AS A  
       LEFT JOIN membermaster B ON B.SKEWCode = A.SKEWCode
       LEFT JOIN miscparameter C ON C.ParameterCode = B.MemberType
       LEFT JOIN palletdismentlecustomerdt D ON D.MemberCode = B.MemberCode
       LEFT JOIN palletdismentlecustomer E ON E.CustDismentleCode = D.CustDismentleCode
       LEFT JOIN palletdispatcheddtl F ON F.MemberCode = B.MemberCode
       LEFT JOIN palletdispatched G ON G.PalletDispCode = F.PalletDispCode
       LEFT JOIN shiptolocation AS H ON H.ShipToLocCode = G.ShipToLocation
       LEFT JOIN membercollectiondtl I ON I.MemberCode = B.MemberCode
       LEFT JOIN membercollection J ON J.CollectionCode = I.CollectionCode
       WHERE D.ACTIVE = 'True'
       AND J.CollectionCode = P_CollectionCode
       AND D.ActionType != 'Y'
       AND D.ACTIVE = 'True'
       AND F.Active = 'True'
       GROUP BY A.MemberSize, SizeDisplayOrder, D.ClientBatchNo
 ) AS subquery
 GROUP BY ClientBatchNo, ActionType;
 
 SELECT SUM(Qty) AS TotalQty
 FROM (
      SELECT COUNT(*) AS Qty       
      FROM membercollectiondtl AS D
      LEFT JOIN membercollection E ON E.CollectionCode = D.CollectionCode
      WHERE D.ACTIVE = 'True'
      AND E.CollectionCode = P_CollectionCode
      GROUP BY E.CollectionCode
 ) AS subquery 
 GROUP BY Qty;
 
END$$

DELIMITER ;
