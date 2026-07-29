import type { MasterConfig } from "./MasterScreen";

/**
 * Every simple master is a config here rather than a hand-written screen.
 * Retail-chain containment runs: Site → Zone → Rack → Tray → Carton → Item,
 * with Stores as the destinations goods are despatched to.
 */
export const MASTER_CONFIGS: Record<string, MasterConfig> = {
  m_product: {
    key: "product",
    title: "Products",
    blurb: "The catalogue. Units per carton and how those units are identified drive item-level counting.",
    idField: "productId",
    fields: [
      { name: "gtin", label: "GTIN", required: true, placeholder: "14 digits" },
      { name: "sku", label: "SKU" },
      { name: "name", label: "Product name", required: true, width: "wide" },
      { name: "category", label: "Category" },
      { name: "brand", label: "Brand" },
      { name: "unitsPerCarton", label: "Units per carton", type: "number" },
      { name: "itemIdentification", label: "Unit identification", type: "select",
        options: ["Visual", "Barcoded", "Mixed"] },
      { name: "uom", label: "UoM" },
      { name: "isActive", label: "Active", type: "checkbox" },
    ],
  },

  m_store: {
    key: "store",
    title: "Stores",
    blurb: "Retail outlets goods are delivered to. Each order line is destined for one store.",
    idField: "storeId",
    fields: [
      { name: "storeCode", label: "Store code", required: true },
      { name: "name", label: "Store name", required: true, width: "wide" },
      { name: "addressLine", label: "Address", width: "wide", inGrid: false },
      { name: "city", label: "City" },
      { name: "region", label: "Region" },
      { name: "postCode", label: "Post code" },
      { name: "country", label: "Country", inGrid: false },
      { name: "isActive", label: "Active", type: "checkbox" },
    ],
  },

  m_zone: {
    key: "zone",
    title: "Zones",
    blurb: "Areas within a distribution centre — pick face, dispatch, goods in. Racks belong to a zone.",
    idField: "zoneId",
    fields: [
      { name: "siteCode", label: "Site code", required: true },
      { name: "zoneCode", label: "Zone code", required: true },
      { name: "name", label: "Zone name", required: true, width: "wide" },
      { name: "zoneType", label: "Type", type: "select",
        options: ["Storage", "PickFace", "Dispatch", "GoodsIn", "Staging"] },
      { name: "isActive", label: "Active", type: "checkbox" },
    ],
  },

  m_rack: {
    key: "rack",
    title: "Racks",
    blurb: "Rack positions that hold trays while they wait in the DC — so a picker can be told exactly where a tray is.",
    idField: "rackId",
    fields: [
      { name: "rackCode", label: "Rack code", required: true, placeholder: "R-A12" },
      { name: "siteCode", label: "Site code", required: true },
      { name: "zoneId", label: "Zone", lookup: "zone", lookupValue: "zoneId", lookupLabel: "name" },
      { name: "aisle", label: "Aisle" },
      { name: "level", label: "Level" },
      { name: "capacity", label: "Tray capacity", type: "number" },
      { name: "isActive", label: "Active", type: "checkbox" },
    ],
  },

  m_vehicle: {
    key: "vehicle",
    title: "Vehicles",
    blurb: "The delivery fleet. Trips are assigned to a vehicle.",
    idField: "vehicleId",
    fields: [
      { name: "registration", label: "Registration", required: true },
      { name: "description", label: "Description", width: "wide" },
      { name: "trayCapacity", label: "Tray capacity", type: "number" },
      { name: "isActive", label: "Active", type: "checkbox" },
    ],
  },

  m_device: {
    key: "device",
    title: "Devices",
    blurb: "Handhelds, edge cameras and telematics units that submit scans.",
    idField: "deviceId",
    fields: [
      { name: "deviceCode", label: "Device code", required: true },
      { name: "deviceType", label: "Type", type: "select",
        options: ["Handheld", "EdgeCamera", "Telematics", "Api"] },
      { name: "siteCode", label: "Site code" },
      { name: "isActive", label: "Active", type: "checkbox" },
    ],
  },

  m_role: {
    key: "role",
    title: "Roles",
    blurb: "Job roles. What each role may do on each screen is set under Role Mapping.",
    idField: "roleId",
    fields: [
      { name: "roleName", label: "Role name", required: true },
      { name: "description", label: "Description", width: "wide" },
      { name: "isAdmin", label: "Full admin", type: "checkbox" },
      { name: "isActive", label: "Active", type: "checkbox" },
    ],
  },
};
