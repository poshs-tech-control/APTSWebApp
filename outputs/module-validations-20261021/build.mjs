import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const out = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(out, '../..', 'APTS_WEB_APP');
const modules = [];
let current;
function module(name, code, scope, note='') { current={name,code,scope,note,rows:[]}; modules.push(current); }
function rule(field, validation, sample, message, layer, file, needle=message, note='') { current.rows.push({field,validation,sample,message,layer,file,needle,note}); }
const C=n=>`Controllers/${n}Controller.cs`, J=n=>`Scripts/apts-${n}.js`, V=n=>`Views/${n}.cshtml`, D='DataIntraction/DataAccess.cs';
const S='Server', B='Browser', BS='Browser + server';

module('Login and Access','LOG','Login, location access and menu visibility');
for(const [f,r,s,m] of [
 ['Login Id','Login Id is required.','Leave Login Id blank.','Please enter Login Id.'],
 ['Login Id length','Maximum 50 characters.','Enter 51 characters.','Login Id cannot exceed 50 characters.'],
 ['Login Id characters','Only letters, digits, underscore, dot, @, hyphen and spaces.','Enter USER#01.','Login Id contains invalid characters.'],
 ['Password','Password is required.','Leave password blank.','Please enter Password.'],
 ['Password length','Maximum 100 characters.','Enter 101 characters.','Password cannot exceed 100 characters.'],
 ['Location','Location is required.','Leave location blank.','Please select Location.'],
 ['Location length','Location code maximum 50 characters.','Submit a 51-character location code.','Location cannot exceed 50 characters.'],
 ['Location characters','Location code permits letters, digits, underscore, hyphen and spaces.','Submit LOC#01.','Location contains invalid characters.']
]) rule(f,r,s,m,S,'Models/LoginInputModel.cs');
rule('Location lookup','Selected location must exist in the loaded location list.','Submit an unknown location code.','Please select valid Location.',S,C('Home'));
rule('Credentials','Login Id and password must match a valid user.','Use an incorrect password.','Invalid Login Id or Password.',S,C('Home'));
rule('Location permission','User must have access to the selected location.','Choose a location without user access.','Access not granted for selected location.',S,C('Home'));
rule('Failed login lockout','After 5 failed attempts, further login attempts are locked for 15 minutes.','Fail login 5 times, then try again.','Account locked due to multiple failed login attempts. Please try again after [minutes] minute(s).',S,C('Home'),'Account locked due to multiple failed login attempts.','Lockout uses the application cache.');
rule('Login request token','Login POST requires an anti-forgery token.','Submit login without a valid token.','Request rejected by anti-forgery validation.',S,C('Home'),'[ValidateAntiForgeryToken]');
rule('Menu visibility','Only menu cards matching the user menu access aliases are displayed.','Login as a user with no assigned menus.','No menu access assigned.',B,V('Menu/Menu'),'No menu access assigned.','This row describes menu visibility, not endpoint authorization.');

module('Member Master','MM','New members and existing QR batch mapping');
rule('Member Type','Member mode must be selected.','Submit without selecting Member Type.','Please select Member Type.',BS,C('MemberMaster'));
rule('Plain text fields','HTML markup is rejected in member fields, SKU, QR and batch values.','Enter <b>ABC</b> in New Batch No.','HTML markup is not allowed in New Batch No.',BS,C('MemberMaster'));
rule('SKU','SKU is required for new-member mode.','Select new-member mode and leave SKU blank.','Please select SKU.',BS,C('MemberMaster'));
rule('SKU lookup','Selected SKU must have available master details.','Submit a SKU with no matching details.','SKU details not found.',S,C('MemberMaster'));
rule('New Batch No','Scan or enter the new batch number.','Leave New Batch No blank.','Please scan New Batch No.',BS,C('MemberMaster'));
rule('Batch master','Batch-number master must be available.','Validate a batch when the master table is unavailable.','New batch number master table not found.',S,C('MemberMaster'));
rule('Batch upload','New batch number must exist in the uploaded batch master.','Scan an unuploaded batch number.','New Batch No not uploaded.',S,C('MemberMaster'));
rule('Batch active flag','Uploaded batch number must be active.','Scan an inactive batch.','New Batch No is not active.',S,C('MemberMaster'));
rule('Batch used flag','New batch number must not already be used.','Scan a used batch.','New Batch No already used.',S,C('MemberMaster'));
rule('Batch uniqueness','New batch number must not already exist in Member Master.','Scan an existing member batch.','New Batch No Already Exist.',S,C('MemberMaster'));
rule('Concurrent batch use','Batch reservation must succeed before saving.','Two requests try to consume the same batch.','New Batch No already used.',S,C('MemberMaster'),'TryMarkNewBatchNumberUsed');
rule('Existing QR input','Existing-with-QR mode requires a QR value.','Choose existing-with-QR mode and leave QR blank.','Please scan or enter QR Code.',S,C('MemberMaster'));
rule('Show existing details','Existing QR details must be loaded using Show before saving.','Scan an existing QR but do not press Show.','Please press Show after scanning QR Code.',B,J('member-master'));
rule('Existing QR mapping','Scanned QR must resolve to an original RFID.','Scan an unmapped QR in existing mode.','Existing RFID not found for scanned QR.',S,C('MemberMaster'));
rule('Existing member','Original RFID must exist in Member Master.','Use a mapped QR whose member record is missing.','Existing Batch No not found in Member Master.',S,C('MemberMaster'));
rule('Existing tagging details','Existing member tagging details must be available for batch mapping.','Map a member without tagging details.','Member tagging details not found.',S,C('MemberMaster'));

module('Member Tagging','MT','Member plate lookup and QR sticker tagging','Member Tagging exists as a controller/view even though it is not a card on the current main menu.');
rule('Member Plate','Scan or enter Member Plate.','Leave Member Plate blank.','Please scan or enter Member Plate.',BS,C('MemberTagging'));
rule('Manufactured member','Plate must identify a manufactured member.','Scan a plate missing from manufactured members.','Member Not Manufactured',S,C('MemberTagging'));
rule('QR Sticker','Scan or enter QR Sticker.','Leave sticker blank.','Please scan or enter QR Sticker.',BS,C('MemberTagging'));
rule('Member lookup before sticker','Load a MemberCode before adding stickers.','Try adding a sticker before loading a plate.','Please scan member plate and get MemberCode.',B,J('member-tagging'));
rule('Sticker duplicate in list','Same sticker cannot be added twice, ignoring case.','Add QR001, then qr001.','Sticker already added in list.',B,J('member-tagging'));
rule('Existing sticker','Sticker must not already exist in member tagging.','Scan an already registered sticker.','Member Already Exist.',S,C('MemberTagging'));
rule('Sticker list','At least one QR Sticker is required for save.','Load plate but save without stickers.','Please add at least one QR Sticker.',BS,D);
rule('Save member identification','Save requires a MemberCode.','Submit tagging without MemberCode.','Please scan member plate and get MemberCode.',S,D);

module('Internal Transfer','IT','Member scan, locations and dispatch details');
rule('Member scan','Scan a member before validation.','Leave scan blank.','Please scan member.',BS,C('InternalTransfer'));
rule('Current location','Current location must be supplied for member validation.','Validate with no current location.','Please select location.',BS,C('InternalTransfer'));
rule('Member mapping','Member must be found and tagged.','Scan an unknown sticker.','Member Not Tag / Member Not Found',S,D,'public InternalTransfer.MemberValidationResult ValidateInternalTransferMember');
rule('Member status','Member must pass the Manufacture / Received status validation procedure.','Scan a member in an ineligible status.','Member status should be Manufacture or Received.',S,D,'Member status should be Manufacture or Received.','Current location is passed to the database procedure; its SQL definition is not present locally.');
rule('Transfer locations','From/To locations and both location types are required.','Submit with To Location blank.','Please select transfer locations.',S,C('InternalTransfer'));
rule('Different locations','From and To cannot be the same location and type.','Choose Factory A as both From and To.','From and To location cannot be same.',BS,C('InternalTransfer'));
rule('Duplicate member','Do not add a member already in the scan list.','Scan the same member twice.','Member already added.',B,J('internal-transfer'));
rule('Member selection','At least one member must be selected.','Continue or save with no members.','Please select member.',BS,C('InternalTransfer'));
rule('Truck selection','Select a truck in Transport Master mode.','Leave Truck List unselected in Master mode.','Please select Truck.',B,J('internal-transfer-dispatch'));
for(const [f,m] of [['Truck Number','Please enter Truck Number.'],['Driver Name','Please enter Driver Name.'],['Driver Contact No.','Please enter Driver Contact No.'],['Transporter Name','Please enter Transporter Name.']]) rule(f,`${f} is required.`,`Leave ${f} blank.`,m,BS,C('InternalTransfer'));

module('Member Receipt','MR','Receipt location, scans, discrepancy and receipt save');
rule('Receipt location','Select a receipt location before proceeding.','Continue with location blank.','Please select Location.',B,J('member-receipt'));
rule('Receipt location on save','Save requires the receipt location.','Submit with no receipt location.','Please select receipt location.',S,C('MemberReceipt'));
rule('Member scan','Scan a member before validation.','Leave scan blank.','Please scan member.',BS,C('MemberReceipt'));
rule('Tagged member','Member must exist and have a mapped tag.','Scan an unknown QR sticker.','Member Not Tag / Member Not Found',S,D,'public MemberReceipt.ReceiveValidationResult ValidateMemberReceiptMember');
rule('Receipt status','Member must have eligible receive data/status.','Scan a member without eligible receipt data.','Member Status Not Match',S,D,'Member Status Not Match');
rule('Location match','Member receive data must match the selected receipt location.','Select Location A for members destined for Location B.','Member Location Not Match',S,D,'public MemberReceipt.ReceiveValidationResult ValidateMemberReceiptMember');
rule('Duplicate member','Repeated scans do not add duplicate RFID rows.','Scan the same member twice.','Existing row retained; duplicate row is not added.',B,J('member-receipt'),'if (!alreadyAdded)');
rule('Discrepancy member','Select discrepancy members before opening discrepancy entry.','Open discrepancy entry without selecting members.','Please select discrepancy member.',B,J('member-receipt'));
rule('Discrepancy action','Select Loss or Damage before applying discrepancy details.','Apply discrepancy without choosing an action.','Please select discrepancy action.',B,J('member-receipt'));
rule('Members to receive','At least one checked member is required for save.','Submit with no checked members.','Please select member.',BS,C('MemberReceipt'));
rule('Dispatch reference','Receipt members must provide at least one dispatch code.','Submit members without dispatch references.','Dispatch code not found.',S,D);

module('Pallet Order','PO','SKU matching, order line selection and order submit','No explicit positive quantity or numeric dimension validation was found on the active order entry/save path.');
rule('SKU matching inputs','Shape, width and length must be supplied for SKU search.','Search with width blank.','Enter pallet shape, width and length.',S,C('PalletOrder'));
rule('Matching SKU','Search needs a matching SKU to select a line.','Search dimensions with no matching SKU.','No matching SKU found for the selected details.',B,J('pallet-order'));
rule('SKU selection','Select at least one SKU before adding order lines.','Click Add without checking a SKU.','Please select at least one SKU.',B,J('pallet-order'));
rule('Order line list','At least one line item is required for submit.','Submit an empty order.','Please add at least one line item.',BS,C('PalletOrder'));
rule('Line SKU code','Rows with empty SKU codes are excluded when building save lines.','Post only line rows with blank SKU codes.','Please add at least one line item.',S,C('PalletOrder'),'if (string.IsNullOrWhiteSpace(skuCodes[i]))');
rule('Session location','Session must contain LocationCode.','Submit after LocationCode is missing.','Location is not found in session.',S,C('PalletOrder'));
rule('Session customer','Session must contain CustomerCode.','Submit after CustomerCode is missing.','Customer is not found in session.',S,C('PalletOrder'));
rule('Saved order details','Saved order must contain at least one successful detail record.','Save returns an order code but zero saved detail lines.','Unable to submit pallet order details.',S,C('PalletOrder'));
rule('Instructions length','Instruction input allows a maximum of 100 characters.','Type 101 characters in Instruction.','Input is limited to 100 characters.',B,V('PalletOrder/PalletOrder'),'maxlength="100"');

module('Pallet Assemble','PA','Pallet order, member eligibility, sizes, quantities and bypass');
rule('Order selection before scan','Select a pallet order before adding members.','Scan without an order selected.','Please select a pallet order before adding members.',B,J('pallet-assemble'));
rule('Member scan','Member QR must be entered or scanned.','Leave member QR blank.','Please scan or enter a member QR code.',BS,C('PalletAssemble'));
rule('Tag mapping','Member must exist and tag must be mapped.','Scan an unmapped sticker.','Member not found or tag is not mapped.',S,D);
rule('Eligible current member','Member must be returned by current-member eligibility lookup.','Scan a member without eligible current-member data.','Member status not valid.',S,D,'Message = "Member status not valid."','Do not infer an exact allowed-status list from the browser fallback message.');
rule('Duplicate member','A member already in the scanned list cannot be added again.','Scan the same RFID twice.','This member is already added in the scanned members list.',B,J('pallet-assemble'));
rule('Allowed member size','Member size must match one of the selected order sizes.','Order needs size 1200; scan size 1500.','Member size [size] does not match this order. Allowed sizes: [sizes].',B,J('pallet-assemble'),'does not match this order. Allowed sizes:');
rule('Quantity by size','Do not exceed the required quantity for CM, LM1 or LM2 size allocation.','Required CM quantity is complete; scan another CM.','[type] size [size] required quantity is already complete.',B,J('pallet-assemble'),'required quantity is already complete.');
rule('Total quantity cap','Do not add more members once order total quantity is complete.','Order needs 5 members; attempt a sixth.','Required member quantity is already complete for this pallet order.',B,J('pallet-assemble'));
rule('Order selection before save','Select a pallet order before submitting assembly.','Submit without a selected order.','Please select a pallet order before submit.',B,J('pallet-assemble'));
rule('Nonempty scan list','At least one scanned member is required.','Submit without scanning any member.','Please scan at least one member.',BS,D);
rule('Complete order quantity','When TotalMemQty is positive, scan count must equal that total.','Order needs 5 members; submit with 4.','Please scan all required members before submit.',B,J('pallet-assemble'));
rule('Recent existing assembly','Member already in Pallet Assemble within the 3-day window is rejected.','Scan a recently assembled member at the same location.','Member already in Pallet Assemble status.',S,D);
rule('Recent assembly and location','Recent Pallet Assemble member with location mismatch cannot bypass.','Recently assembled member has a different last location.','Pallet created within 3 days. Location mismatch bypass is not allowed.',S,D);
rule('Recent pallet reuse','Other-status member in the 3-day created-pallet window is rejected.','Scan a recently used member now in another status.','Pallet already created within 3 days',S,D);
rule('Location bypass','Eligible location mismatch opens a confirmation before updating last location.','Scan an eligible member last located elsewhere.','Member location ([last]) does not match selected location ([current]). Do you want to update the member\'s last location and continue?',B,J('pallet-assemble'),'does not match selected location','Confirmed bypass marks a violation and updates member location/state.');
rule('Bypass 3-day check','Bypass rechecks the 3-day window before updating the member.','Continue bypass for a member within the restricted window.','Pallet created within 3 days. Pallet Assemble is not allowed.',S,D);
rule('Client location on save','Assembly save requires ClientLocation.','Post assembly without ClientLocation.','Client location is required.',S,D);
rule('Order detail on save','Assembly save requires PalletOrderdtlCode.','Post assembly without order detail code.','Pallet order details are required.',S,D);

module('Pallet Diss Client','PDC','Client pallet disassembly');
rule('Member scan','Member scan is required.','Validate with a blank member.','Please scan member.',S,C('PalletDisassembleClient'));
rule('Member status','Scanned member must be in Pallet Assemble status.','Scan a member in another status.','Member status should be Pallet Assemble.',S,D);
rule('Pallet details','Pallet details must be available for the scanned member.','Scan an assembled member without pallet detail data.','Pallet details not found.',S,D);
rule('Duplicate member','Member already loaded cannot be added twice.','Scan the same member again.','Member [RFID] already added.',B,J('pallet-disassemble-client'),'already added.');
rule('Loaded member details','Scan must return pallet member details.','Validation returns no pallet members.','Pallet member details not found.',B,J('pallet-disassemble-client'));
rule('Member selection','Select at least one member to disassemble.','Submit with no selected members.','Please select member.',BS,C('PalletDisassembleClient'));
rule('Pallet reference','At least one selected member must have a pallet code.','Submit members without pallet references.','Pallet code not found.',S,D);

module('Pre-Dispatch Details','PD','Member validation, 3-day eligibility and pre-dispatch entry');
rule('Member scan','Member scan is required before validation.','Validate with a blank scan.','Please scan member.',BS,C('PalletPreDispatch'));
rule('Assemble status','Member must be available at Pallet Assemble.','Scan a member in another status.','Member is not available at Pallet Assemble.',S,D);
rule('Assembly date lookup','Assembly-date validation data must be available.','Scan a member with no date-validation record.','Member assemble date validation not found.',S,D);
rule('3-day window','Date-validation procedure must return ALLOWED.','Scan a member assembled more than 3 days ago.','Member assembled more than 3 days ago. Pre-Dispatch not allowed.',S,D,'Member assembled more than 3 days ago. Pre-Dispatch not allowed.','Exact boundary is delegated to the database procedure.');
rule('Validated pallet','A validated PalletCode is required on save.','Submit without validating a member.','Please validate member first.',S,C('PalletPreDispatch'));
rule('Client Batch No','Client Batch No is required on save.','Leave Client Batch No blank.','Please enter Client Batch No.',S,C('PalletPreDispatch'));
rule('Storage Location','Storage Location is required.','Leave Storage Location blank.','Please enter Storage Location.',S,C('PalletPreDispatch'));
rule('Existing-entry lookup key','PalletCode or ClientBatchNo is required for duplicate lookup.','Check an existing entry with both keys blank.','Pallet Code or Client Batch No is required.',S,C('PalletPreDispatch'));
rule('Existing pre-dispatch','Existing pre-dispatch is identified by pallet or client batch.','Check a pallet/batch already in pre-dispatch.','Pallet Pre-Dispatch already exists.',BS,C('PalletPreDispatch'),'Pallet Pre-Dispatch already exists.','The browser identifies an existing entry; this is not a claim of a unique database constraint.');
rule('Numeric inputs','Weight and Thickness inputs have minimum 0 and step 0.01.','Enter Weight -1 or Thickness -0.5.','Browser numeric input constraint.',B,V('PalletPreDispatch/PalletPreDispatch'),'id="Weight"','The save controller defaults blank numeric fields to 0 and has no explicit negative-value check.');

module('Pallet Dispatch','PDS','Member / batch scans, customer and consignee selection','Truck and driver fields have no explicit required check in the current dispatch view/controller.');
rule('To Customer','Select the receiving customer.','Continue with To Customer blank.','Please select To Customer.',BS,C('PalletDispatch'));
rule('Consignee','Select Consignee.','Continue with Consignee blank.','Please select Consignee.',BS,C('PalletDispatch'));
rule('Scan input','Member scan cannot be blank.','Validate with a blank scan.','Members Not in PreDispatch.',S,C('PalletDispatch'));
rule('Pre-dispatch member eligibility','Member must pass dispatch validation for pre-dispatch.','Scan a member not eligible for dispatch.','Members Not in PreDispatch.',S,D,'public PalletDispatcch.DispatchValidationResult ValidateDispatchMember','Validation result is returned by the database procedure.');
rule('Client batch input','Client Batch No must be supplied in batch scan mode.','Add an empty client batch.','Enter Client Batch No.',B,J('pallet-dispatch'));
rule('Batch lookup','Batch list must resolve to available dispatch batches.','Lookup a batch with no pre-dispatch details.','Some Members are not In PreDispatch.',S,C('PalletDispatch'));
rule('Duplicate scan','Do not add an already loaded dispatch item again.','Add the same member/batch twice.','Already added.',B,J('pallet-dispatch'));
rule('Dispatch item identifiers','Save needs a line with PalletCode and ClientBatchNo, neither blank nor hyphen.','Post only rows with missing pallet/batch identifiers.','Please add dispatch batches.',S,C('PalletDispatch'));
rule('Previously dispatched members','Reject a pallet whose members are already dispatched.','Submit a pallet already dispatched.','Already Dispatch',S,C('PalletDispatch'));
rule('Session From Location','Session must provide the dispatch FromLocation.','Save after the session location is lost.','Session From Location not found. Please login again.',S,C('PalletDispatch'));

module('Pallet Diss Customer','PDCO','Pallet Dispatch / Other modes, discrepancy, temporary and final save');
rule('To Customer','Select a customer before disassembling or final save.','Leave To Customer blank.','Please select To Customer.',BS,C('PalletDisassembleCustomer'));
rule('Member scan','Scan a member before validation.','Leave member input blank.','Please scan member.',BS,C('PalletDisassembleCustomer'));
rule('Tag mapping','Member must exist and be tagged.','Scan an unknown sticker.','Member Not Tag / Member Not Found',S,D,'public PalletDisassembleCustomer.ValidationResult ValidatePalletDisassembleCustomerMember');
rule('Pallet Dispatch mode status','In Pallet Dispatch mode, member must be in Pallet Dispatch status.','Use this mode with an ineligible member.','Member status should be Pallet Dispatch.',S,D);
rule('Dispatch details','Pallet dispatch details must be available.','Scan a dispatched member without detail rows.','Pallet dispatch details not found.',S,D);
rule('One active pallet','Finish disassembling the loaded pallet before loading a different pallet.','Load Pallet A, then scan a member from Pallet B.','Pallet [code] is already loaded. Please disassemble this pallet before scanning members from another pallet.',B,J('pallet-disassemble-customer'),'is already loaded. Please disassemble');
rule('Duplicate member','Do not add an already loaded member again.','Scan the same member twice.','Member [RFID] already added.',B,J('pallet-disassemble-customer'),'already added.');
rule('Other mode already disassembled','Other mode rejects members already in Pallet Disassemble from Customer.','Use Other mode on an already disassembled member.','Member is in Pallet Disassemble from Customer',S,D);
rule('Other mode dispatched member','A Pallet Dispatch member must be added using Pallet Dispatch mode.','Use Other mode for a dispatched member.','Member in Pallet Dispatch, Please select the pallet dispatch option to add the member',S,D);
rule('Other mode manufactured member','Other mode needs a manufactured member with type and size data.','Use Other mode with a missing manufactured record.','Member not manufactured',S,D);
rule('Discrepancy member','Select a discrepancy member before entering discrepancy details.','Open discrepancy entry with nothing checked.','Please select discrepancy member.',B,J('pallet-disassemble-customer'));
rule('Discrepancy action','Select an action before applying discrepancy.','Apply with neither Loss nor Damage selected.','Please select discrepancy action.',B,J('pallet-disassemble-customer'));
rule('Discrepancy completeness','Checked discrepancy members require Loss or Damage before disassembly.','Disassemble while a checked discrepancy has no action.','Please select Loss or Damage for checked discrepancy member.',B,J('pallet-disassemble-customer'));
rule('Member selection','At least one member is required for temporary disassembly save.','Submit disassembly without members.','Please select member.',S,C('PalletDisassembleCustomer'));
rule('Temporary records before final save','At least one temporary disassembly code is required for final save.','Click final save before disassembling a pallet.','Please disassemble pallet first.',BS,C('PalletDisassembleCustomer'));
rule('Detail lookup code','A disassemble code is required to load saved temporary details.','Request details with blank disassemble code.','Disassemble code not found.',S,C('PalletDisassembleCustomer'));

module('Member Collection','MC','Customer member collection and transport details');
rule('Disassemble From','Select the customer from whom members are collected.','Leave customer blank.','Please select Disassemble from.',BS,C('MemberCollection'));
rule('Member scan','Scan or enter a member.','Leave scan blank.','Please scan or enter a member.',BS,C('MemberCollection'));
rule('Tagged member','Member must exist and be tagged.','Scan an unknown sticker.','Member Not Tag / Member Not Found',S,C('MemberCollection'));
rule('Member status','Only eligible customer-disassembled or customer-disassembled-damage member data can be added.','Scan a member in Pallet Dispatch status.','Member status must be Pallet Disassemble from Customer or Pallet Disassemble from Customer-Damage.',S,C('MemberCollection'));
rule('Customer matching','Returned nonblank member customer codes must match the selected customer.','Select Customer A, scan a member of Customer B.','Member does not belong to the selected customer.',S,C('MemberCollection'),'Member does not belong to the selected customer.','Blank returned customer codes are not rejected by this comparison.');
rule('Duplicate member','Repeated member cannot be added again.','Scan the same member twice.','Member already added.',B,J('member-collection'));
rule('Nonempty member list','At least one valid RFID member is needed to proceed and save.','Submit with an empty member list.','Please add at least one member.',BS,C('MemberCollection'));
rule('Truck selection','Select a truck in Transport Master mode.','Choose Master mode but no truck.','Please select Truck.',B,J('member-collection-details'));
for(const [f,m] of [['Truck Number','Please enter Truck Number.'],['Driver Name','Please enter Driver Name.'],['Driver Contact No.','Please enter Driver Contact No.'],['Transporter Name','Please enter Transporter Name.']]) rule(f,`${f} is required.`,`Leave ${f} blank.`,m,BS,C('MemberCollection'));
rule('RFID cleanup','Blank RFID rows are removed; duplicate RFID rows are collapsed ignoring case.','Post RFID A twice and a blank RFID row.','Only one valid A row remains.',S,C('MemberCollection'),'private static List<MemberCollection.CollectionMemberItem> CleanMembers');
rule('Customer dismantle code','At least one customer dismantle reference is required to save collection.','Post members with all customer dismantle codes blank.','Customer dismantle code not found.',S,D);

module('Collection Internal Transfer','CIT','Collection truck, bulk member scans, bypass and dispatch');
rule('Member scan','Member scan is required.','Leave member input blank.','Please scan member.',BS,C('CollectionInternalTransfer'));
rule('Truck before scan','Select a master truck or enter a truck in Other mode.','Scan without selecting or entering a truck.','Please select truck. / Please enter Truck Number.',B,J('collection-internal-transfer'),'Please select truck.');
rule('To Location','Select To Location before proceeding.','Continue with To Location blank.','Please select To location.',B,J('collection-internal-transfer'));
rule('Member mapping','Scanned member must resolve to a tagged member.','Scan an unknown QR.','Member Not Tag / Member Not Found',S,D,'public InternalTransfer.CollectionBulkValidationResult ValidateCollectionInternalTransferMember');
rule('Already transferred status','Member already in Internal Transfer or Internal Transfer-Damage is rejected without bypass.','Scan an already internally transferred member.','Member Already In Internal Transfer',S,D);
rule('Collection status bypass','Ineligible Member Collection status prompts for confirmation to continue.','Scan a known member outside Member Collection status.','Member is not in Member Collection status. Do you want to continue?',S,D,'Member is not in Member Collection status.','Confirmed bypass creates a violation mark.');
rule('Collection truck match','For eligible collection members, collected truck must match selected truck code or number.','Member was collected in Truck A; select Truck B.','Truck Mismatch, Member Were Collected in different Truck. Please Select The Correct Truck',S,D);
rule('Duplicate member','Do not add a member already in the list.','Scan the same RFID twice.','Member already added.',B,J('collection-internal-transfer'));
rule('Member selection','At least one member is needed to continue and save.','Submit with no members.','Please select member.',BS,C('CollectionInternalTransfer'));
rule('Transfer locations','From/To location and type values must be supplied.','Submit with missing transfer location/type.','Please select transfer locations.',S,C('CollectionInternalTransfer'));
rule('Different locations','Same From/To location with same type is rejected.','Submit identical source and destination.','From and To location cannot be same.',S,C('CollectionInternalTransfer'));
rule('Truck Number','Truck Number is required for save.','Leave Truck Number blank.','Truck Number is required.',BS,C('CollectionInternalTransfer'));
for(const [f,m] of [['Driver Name','Driver Name is required.'],['Driver Contact No.','Driver Contact No. is required.'],['Transporter Name','Transporter Name is required.']]) rule(f,`${f} is required by the details form.`,`Leave ${f} blank.`,m,B,J('collection-internal-transfer-dispatch'),m,'No matching required-field check in SubmitCollectionInternalTransfer.');
rule('Bypass RFID','Bypass request requires RFID.','Continue bypass with blank RFID.','Member RFID is required.',S,D,'public InternalTransfer.CollectionBulkValidationResult ContinueCollectionInternalTransferBypass');
rule('Bypass member record','Bypass needs an existing member record.','Continue bypass for a missing member.','Member details not found.',S,D,'LoadCollectionInternalTransferAnyMember');

module('Current Stock Report','CSR','Current-stock report filters');
rule('Location','Select a stock location.','Run report with no location.','Please select location.',S,C('Report'),'public JsonResult GetCurrentStockReport');
rule('Report date','A report date must be supplied.','Run report with date blank/default.','Please select date.',S,C('Report'));

module('Query Report','QR','Member query lookup');
rule('Member RFID','Scan or enter member RFID.','Run query with empty RFID.','Please scan or enter member RFID.',S,C('Report'));
rule('Existing member data','Lookup must return an item with RFID.','Query an unknown member.','No data found for scanned member.',S,C('Report'));

module('Discrepancy Report','DR','Discrepancy type and report date range');
rule('Discrepancy Type','Discrepancy type is required.','Run report without discrepancy type.','Please select discrepancy type.',S,C('Report'));
rule('Date range','Both From Date and To Date must be supplied.','Leave either date blank.','Please select from date and to date.',S,C('Report'),'public JsonResult GetDiscrepancyReport');
rule('Date sequence','From Date cannot be later than To Date.','From 21-Oct-2026; To 20-Oct-2026.','From date cannot be greater than to date.',S,C('Report'),'public JsonResult GetDiscrepancyReport');

module('High Age Report','HAR','Location type, location and aging days');
rule('Report location type','High Age report location type is required.','Run report with type blank.','Please select report location type.',S,C('Report'));
rule('Location','Report location is required.','Run report with location blank.','Please select location.',S,C('Report'),'public JsonResult GetHighAgeReport');
rule('Total days','Total Days must parse as an integer and be zero or greater.','Enter -1, 1.5 or ABC.','Please enter valid total days.',S,C('Report'));

module('Member Transaction Report','MTR','Location and report date range');
rule('Location','Report location is required.','Run report with location blank.','Please select location.',S,C('Report'),'public JsonResult GetMemberTransactionReport');
rule('Date range','Both From Date and To Date must be supplied.','Leave either date blank.','Please select from date and to date.',S,C('Report'),'public JsonResult GetMemberTransactionReport');
rule('Date sequence','From Date cannot be later than To Date.','From 21-Oct-2026; To 20-Oct-2026.','From date cannot be greater than to date.',S,C('Report'),'public JsonResult GetMemberTransactionReport');

// Read source files once and resolve real line references for every documented rule.
for(const name of ['Current Stock Report','Discrepancy Report','Member Transaction Report']) {
 for(const r of modules.find(m=>m.name===name).rows) r.layer=BS;
}
modules.find(m=>m.name==='High Age Report').rows.find(r=>r.field==='Location').layer=BS;
modules.find(m=>m.name==='High Age Report').rows.find(r=>r.field==='Total days').note='Browser checks blank/negative values; server performs the full integer validation.';
modules.find(m=>m.name==='Member Transaction Report').rows.find(r=>r.field==='Date sequence').note='Same-day From/To dates are permitted.';
modules.find(m=>m.name==='Discrepancy Report').rows.find(r=>r.field==='Date sequence').note='Same-day From/To dates are permitted.';
const sourceCache=new Map();
async function source(file,needle){
 if(!sourceCache.has(file)) sourceCache.set(file,(await fs.readFile(path.join(root,file),'utf8')).split(/\r?\n/));
 const lines=sourceCache.get(file); const i=lines.findIndex(s=>s.includes(needle));
 if(i<0) throw new Error(`Missing source anchor: ${file}: ${needle}`);
 return `${path.basename(file)}:${i+1}`;
}
// Include actual view constraints for the active transport screens.
for(const [name,view,truck,contact] of [
 ['Internal Transfer','InternalTransfer/MemberDispatch','TruckNumber','DriverContactNo'],
 ['Member Collection','MemberCollection/MemberCollectionDetails','CollectionTruckNo','DriverContactNo'],
 ['Pallet Dispatch','PalletDispatch/PalletDispatchDetails','TruckNumber','DriverContactNo'],
 ['Collection Internal Transfer','CollectionInternalTransfer/CollectionInternalTransfer','CollectionTruckNumber',null]
]){
 current=modules.find(m=>m.name===name);
 const text=await fs.readFile(path.join(root,V(view)),'utf8');
 const inputs=[...text.matchAll(/<input\b[\s\S]*?>/g)].map(x=>x[0]);
 for(const inp of inputs.filter(x=>/maxlength="10"/.test(x))){
   const field=/Contact/.test(inp)?'Driver Contact No.':'Truck Number';
   const id=(inp.match(/id="([^"]+)"/)||[])[1];
   rule(`${field} length`,'Input allows a maximum of 10 characters.',`Type 11 characters in ${field}.`,'Input is limited to 10 characters.',B,V(view),`id="${id}"`,field==='Driver Contact No.'?'No exact-10-digit or digits-only check was found in this module.':'No explicit vehicle registration format validation was found.');
 }
}

const workbook=Workbook.create();
const index=workbook.worksheets.add('Module Index');
for(const m of modules) workbook.worksheets.add(m.name);
const headers=['Validation ID','Field / Action','Validation rule','Sample test','Expected message / behavior','Enforced in','Code reference','Notes'];
const widths=[15,26,56,43,69,22,55,54];
function style(sheet,last,cols=8){
 sheet.showGridLines=false;
 const lastCol=String.fromCharCode(64+cols);
 sheet.getRange(`A1:${lastCol}${last}`).format.font={name:'Arial',size:10,color:'#243448'};
 sheet.getRange(`A1:${lastCol}${last}`).format.verticalAlignment='center';
 sheet.getRange(`A5:${lastCol}5`).format={fill:'#223E5C',font:{name:'Arial',size:10,bold:true,color:'#FFFFFF'},horizontalAlignment:'center',verticalAlignment:'center',rowHeight:29,wrapText:true};
 sheet.getRange('A2').format.font={name:'Arial',size:15,bold:true,color:'#223E5C'};
 sheet.getRange(`A3:${lastCol}3`).format.borders={bottom:{style:'thin',color:'#CCD5DF'}};
 sheet.getRange('A3').format.font={name:'Arial',size:10,italic:true,color:'#63758A'};
 for(let c=0;c<cols;c++) sheet.getRange(`${String.fromCharCode(65+c)}1:${String.fromCharCode(65+c)}${last}`).format.columnWidth=widths[c];
 if(last>5){
  sheet.getRange(`A6:${lastCol}${last}`).format.wrapText=true;
  sheet.getRange(`A6:${lastCol}${last}`).format.verticalAlignment='top';
  for(let r=6;r<=last;r++) if(r%2===0) sheet.getRange(`A${r}:${lastCol}${r}`).format.fill='#F0F4F8';
 }
 sheet.freezePanes.freezeRows(5); sheet.freezePanes.freezeColumns(2);
}

let total=0;
for(const [mi,m] of modules.entries()){
 const sh=workbook.worksheets.getItem(m.name);
 const values=[];
 for(const [i,r] of m.rows.entries()){
   values.push([`${m.code}-${String(i+1).padStart(3,'0')}`,r.field,r.validation,r.sample,r.message,r.layer,await source(r.file,r.needle),r.note]);
 }
 total+=values.length;
 sh.getRange('A2').values=[[`${m.name} validations`]];
 sh.getRange('A3').values=[['Sample tests describe expected behavior; they have not been executed.']];
 sh.getRange('A5:H5').values=[headers];
 sh.getRange(`A6:H${5+values.length}`).values=values;
 style(sh,5+values.length);
 const table=sh.tables.add(`A5:H${5+values.length}`,true,`Validations_${m.code}`); table.showFilterButton=true;
 sh.getRange('A5:H5').format.fill='#223E5C';sh.getRange('A5:H5').format.font.color='#FFFFFF';
 for(let i=0;i<values.length;i++){
  const maxLines=Math.max(...values[i].map((v,c)=>String(v).split('\n').reduce((sum,line)=>sum+Math.max(1,Math.ceil(line.length/(widths[c]-3))),0)));
  sh.getRange(`A${i+6}:H${i+6}`).format.rowHeight=Math.max(42,maxLines*14+13);
 }
}
index.getRange('A2').values=[['APTS module validation register']];
index.getRange('A3').values=[['Reviewed 21-Oct-2026. Current local application source.']];
index.getRange('A5:E5').values=[['No.','Module / worksheet','Validation count','Scope','Coverage note']];
index.getRange(`A6:E${5+modules.length}`).values=modules.map((m,i)=>[i+1,m.name,m.rows.length,m.scope,m.note]);
style(index,31,5);
index.getRange('A1:A31').format.columnWidth=8;
index.getRange('B1:B31').format.columnWidth=31;
index.getRange('C1:C31').format.columnWidth=18;
index.getRange('D1:D31').format.columnWidth=62;
index.getRange('E1:E31').format.columnWidth=79;
index.getRange('A6:E23').format.rowHeight=44;
index.tables.add('A5:E23',true,'ModuleCoverage');
index.getRange('A5:E5').format.fill='#223E5C';index.getRange('A5:E5').format.font.color='#FFFFFF';
index.getRange('B25').values=[['Total validations']];index.getRange('C25').formulas=[['=SUM(C6:C23)']];
index.getRange('B25:C25').format.font.bold=true;
index.getRange('B27').values=[['How to use']];index.getRange('D27').values=[['Open a module worksheet. Filter by field or enforcement layer. Sample identifiers are illustrative.']];
index.getRange('B28').values=[['Coverage']];index.getRange('D28').values=[['Includes current menu workflows, Member Tagging, login/access and all five report screens.']];
index.getRange('B29').values=[['Review limits']];index.getRange('D29').values=[['Source-code review only. Database procedures and live application behavior were not executed.']];
index.getRange('B30').values=[['Messages']];index.getRange('D30').values=[['Messages follow source wording. Square brackets show dynamic values. Browser limits may have no popup.']];
index.getRange('B31').values=[['Code references']];index.getRange('D31').values=[['References identify source filenames and line numbers in the reviewed local code.']];
index.getRange('B27:B31').format.font.bold=true;
index.getRange('D27:E31').format.wrapText=true;
index.getRange('D27:D31').format.rowHeight=46;
index.getRange('C6:C25').setNumberFormat('0');
index.tabColor='#223E5C';
workbook.recalculate();
const check=await workbook.inspect({kind:'table',range:'Module Index!A5:E25',include:'values,formulas',tableMaxRows:21,tableMaxCols:5,maxChars:3500});
console.log(check.ndjson);
if(index.getRange('C25').values[0][0]!==total) throw new Error('Module index total mismatch');
console.log((await workbook.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#NUM!|#NULL!',options:{useRegex:true,maxResults:30},summary:'Formula check',maxChars:1000})).ndjson);
await fs.mkdir(path.join(out,'previews'),{recursive:true});
for(const name of ['Module Index',...modules.map(m=>m.name)]){
 const sheet=workbook.worksheets.getItem(name);
 const bottom=name==='Module Index'?31:5+modules.find(m=>m.name===name).rows.length;
 const blob=await workbook.render({sheetName:name,range:`A1:E${bottom}`,scale:1,format:'png'});
 await fs.writeFile(path.join(out,'previews',`${name}.png`),new Uint8Array(await blob.arrayBuffer()));
 console.log(`Rendered: ${name}`);
}
const output=await SpreadsheetFile.exportXlsx(workbook);
await output.save(path.join(out,'APTS_All_Modules_Validation_List.xlsx'));
await fs.writeFile(path.join(out,'validation-register.json'),JSON.stringify(modules,null,2));
console.log(JSON.stringify({modules:modules.length,validations:total,file:path.join(out,'APTS_All_Modules_Validation_List.xlsx')}));
