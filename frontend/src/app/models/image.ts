export interface ImageRecord {
  id: number;
  fileName: string;
  sizeBytes: number;
  width: number;
  height: number;
  uploadedAt: string;
  extension: string;
  contentType: string;
  megapixels: number;
}
