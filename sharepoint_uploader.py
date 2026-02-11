"""Integração SharePoint via Microsoft Graph API"""
import requests
import pandas as pd
import io
import logging
from config import (AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, AZURE_TENANT_ID,
                    SHAREPOINT_DRIVE_ID, SHAREPOINT_PASTA_DESTINO)

logger = logging.getLogger(__name__)


class SharePointUploader:
    """Upload para SharePoint via Graph API"""

    def __init__(self):
        self.drive_id = SHAREPOINT_DRIVE_ID
        self.base_folder = SHAREPOINT_PASTA_DESTINO
        self.access_token = self._get_token()
        self.pasta_id = self._obter_pasta_id()

    def _get_token(self) -> str:
        """Obtém token OAuth2"""
        r = requests.post(
            f"https://login.microsoftonline.com/{AZURE_TENANT_ID}/oauth2/v2.0/token",
            data={
                "client_id": AZURE_CLIENT_ID,
                "client_secret": AZURE_CLIENT_SECRET,
                "grant_type": "client_credentials",
                "scope": "https://graph.microsoft.com/.default"
            }, timeout=10)
        r.raise_for_status()
        logger.info("✅ Autenticação Graph OK")
        return r.json()["access_token"]

    def _headers(self) -> dict:
        return {"Authorization": f"Bearer {self.access_token}", "Accept": "application/json"}

    def _obter_pasta_id(self) -> str:
        """Obtém ou cria pasta de destino"""
        r = requests.get(
            f"https://graph.microsoft.com/v1.0/drives/{self.drive_id}/root/children",
            headers=self._headers(), timeout=10)
        r.raise_for_status()
        
        for item in r.json().get("value", []):
            if item.get("name") == self.base_folder and "folder" in item:
                return item["id"]
        
        return self._criar_pasta(self.base_folder)

    def _criar_pasta(self, nome: str) -> str:
        """Cria pasta na raiz do drive"""
        r = requests.post(
            f"https://graph.microsoft.com/v1.0/drives/{self.drive_id}/root/children",
            headers=self._headers(),
            json={"name": nome, "folder": {}, "@microsoft.graph.conflictBehavior": "rename"},
            timeout=10)
        r.raise_for_status()
        logger.info(f"✅ Pasta '{nome}' criada")
        return r.json()["id"]

    def upload_csv(self, df: pd.DataFrame, filename: str, colunas_ordem: list = None) -> dict:
        """Upload DataFrame como CSV"""
        try:
            df_copy = df.copy()
            
            if colunas_ordem:
                cols = [c for c in colunas_ordem if c in df_copy.columns]
                cols += [c for c in df_copy.columns if c not in colunas_ordem]
                df_copy = df_copy[cols]
            
            # Formatar números BR
            for col in df_copy.select_dtypes(include=['float64', 'float32']).columns:
                df_copy[col] = df_copy[col].apply(
                    lambda x: f"{x:,.2f}".replace(",", "X").replace(".", ",").replace("X", ".") 
                    if pd.notna(x) else "")

            buf = io.StringIO()
            df_copy.to_csv(buf, index=False, encoding="utf-8-sig", sep=";")
            content = buf.getvalue().encode("utf-8-sig")

            r = requests.put(
                f"https://graph.microsoft.com/v1.0/drives/{self.drive_id}/items/{self.pasta_id}:/{filename}:/content",
                headers={**self._headers(), "Content-Type": "application/octet-stream"},
                data=content, timeout=60)
            r.raise_for_status()

            logger.info(f"✅ Upload: {filename} ({len(content)/1024:.1f}KB)")
            return {"status": "sucesso", "url": r.json().get("webUrl", ""), "mensagem": f"Upload: {filename}"}
        
        except Exception as e:
            logger.error(f"❌ Upload falhou: {e}")
            return {"status": "erro", "mensagem": str(e)}

    def upload_parquet(self, df: pd.DataFrame, filename: str) -> dict:
        """Upload DataFrame como Parquet"""
        try:
            buf = io.BytesIO()
            df.to_parquet(buf, index=False, engine="pyarrow", compression="snappy")
            content = buf.getvalue()

            r = requests.put(
                f"https://graph.microsoft.com/v1.0/drives/{self.drive_id}/items/{self.pasta_id}:/{filename}:/content",
                headers={**self._headers(), "Content-Type": "application/octet-stream"},
                data=content, timeout=60)
            r.raise_for_status()

            logger.info(f"✅ Upload: {filename} ({len(content)/1024:.1f}KB)")
            return {"status": "sucesso", "url": r.json().get("webUrl", ""), "mensagem": f"Upload: {filename}"}
        
        except Exception as e:
            logger.error(f"❌ Upload falhou: {e}")
            return {"status": "erro", "mensagem": str(e)}
