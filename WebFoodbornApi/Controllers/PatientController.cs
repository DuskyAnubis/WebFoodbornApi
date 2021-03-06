﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using WebFoodbornApi.Data;
using WebFoodbornApi.Dtos;
using WebFoodbornApi.Common;
using WebFoodbornApi.Filters;
using WebFoodbornApi.Models;
using FoodBornWebService;
using System.Xml;
using System.IO;
using System.Xml.Linq;

namespace WebFoodbornApi.Controllers
{
    [Produces("application/json")]
    [Route("api/v1/Patients")]
    [Authorize]
    public class PatientController : Controller
    {
        private readonly ApiContext dbContext;
        private readonly IMapper mapper;
        private readonly FoodBornApiOptions apiOptions;

        public PatientController(ApiContext dbContext, IMapper mapper, FoodBornApiOptions apiOptions)
        {
            this.dbContext = dbContext;
            this.mapper = mapper;
            this.apiOptions = apiOptions;
        }

        #region 患者基本操作

        #region 获得患者列表
        /// <summary>
        /// 获取患者列表
        /// </summary>
        /// <param name="input">传入参数</param>
        /// <returns>患者列表</returns>
        [HttpGet]
        [ProducesResponseType(typeof(List<PatientOutput>), 200)]
        [ProducesResponseType(typeof(void), 500)]
        public async Task<IEnumerable<PatientOutput>> GetPatients(PatientQueryInput input)
        {
            int pageIndex = input.Page - 1;
            int Per_Page = input.Per_Page;
            string sortBy = input.SortBy;

            IQueryable<Patient> query = dbContext.Patients
                 .AsQueryable();

            query = query.Where(q => string.IsNullOrEmpty(input.PatientName) || q.PatientName.Contains(input.PatientName));
            query = query.Where(q => string.IsNullOrEmpty(input.OutpatientNo) || q.OutpatientNo.Equals(input.OutpatientNo));
            query = query.OrderBy(sortBy);

            var totalCount = query.Count();
            var totalPages = (int)Math.Ceiling((double)totalCount / Per_Page);

            HttpContext.Response.Headers.Add("X-TotalCount", JsonConvert.SerializeObject(totalCount));
            HttpContext.Response.Headers.Add("X-TotalPage", JsonConvert.SerializeObject(totalPages));

            query = query.Skip(pageIndex * Per_Page).Take(Per_Page);

            List<Patient> patients = await query.ToListAsync();
            List<PatientOutput> list = mapper.Map<List<PatientOutput>>(patients);

            return list;
        }
        #endregion

        #region 获得待上传患者列表
        /// <summary>
        /// 获取患者列表
        /// </summary>
        /// <param name="input">传入参数</param>
        /// <returns>患者列表</returns>
        [HttpGet("PendingUpload")]
        [ProducesResponseType(typeof(List<PatientOutput>), 200)]
        [ProducesResponseType(typeof(void), 500)]
        public async Task<IEnumerable<PatientOutput>> GetPendingUploadPatients(PatientQueryInput input)
        {
            int pageIndex = input.Page - 1;
            int Per_Page = input.Per_Page;
            string sortBy = input.SortBy;

            IQueryable<Patient> query = dbContext.Patients
                 .AsQueryable();

            query = query.Where(q => string.IsNullOrEmpty(input.PatientName) || q.PatientName.Contains(input.PatientName));
            query = query.Where(q => string.IsNullOrEmpty(input.OutpatientNo) || q.OutpatientNo.Equals(input.OutpatientNo));
            query = query.Where(q => q.Status.Equals("正常"));
            query = query.OrderBy(sortBy);

            var totalCount = query.Count();
            var totalPages = (int)Math.Ceiling((double)totalCount / Per_Page);

            HttpContext.Response.Headers.Add("X-TotalCount", JsonConvert.SerializeObject(totalCount));
            HttpContext.Response.Headers.Add("X-TotalPage", JsonConvert.SerializeObject(totalPages));

            query = query.Skip(pageIndex * Per_Page).Take(Per_Page);

            List<Patient> patients = await query.ToListAsync();
            List<PatientOutput> list = mapper.Map<List<PatientOutput>>(patients);

            return list;
        }
        #endregion

        #region 获得患者信息
        /// <summary>
        /// 获得用户信息
        /// </summary>
        /// <param name="id">Id</param>
        /// <returns></returns>
        [HttpGet("{id}", Name = "GetPatient")]
        [ProducesResponseType(typeof(PatientOutput), 200)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(void), 500)]
        public async Task<IActionResult> GetPatient([FromRoute]int id)
        {
            Patient patient = await dbContext.Patients
               .FirstOrDefaultAsync(p => p.Id == id);

            if (patient == null)
            {
                return NotFound(Json(new { Error = "该患者不存在" }));
            }

            PatientOutput output = mapper.Map<PatientOutput>(patient);

            return new ObjectResult(output);
        }

        /// <summary>
        /// 获得患者信息
        /// </summary>
        /// <param name="outpatientNo">outpatientNo</param>
        /// <returns></returns>
        [HttpGet("~/api/v1/Patients/OutpatientNo/{outpatientNo}")]
        [ProducesResponseType(typeof(PatientOutput), 200)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(void), 500)]
        public async Task<IActionResult> GetPatientByOutpatientNo([FromRoute]string outpatientNo)
        {
            Patient patient = await dbContext.Patients
               .FirstOrDefaultAsync(p => p.OutpatientNo.Equals(outpatientNo));

            if (patient == null)
            {
                return NotFound(Json(new { Error = "该患者不存在" }));
            }

            PatientOutput output = mapper.Map<PatientOutput>(patient);

            return new ObjectResult(output);
        }
        #endregion

        #region 创建患者
        /// <summary>
        /// 创建患者
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateModel]
        [ProducesResponseType(typeof(PatientOutput), 201)]
        [ProducesResponseType(typeof(ValidationError), 422)]
        [ProducesResponseType(typeof(void), 500)]
        public async Task<IActionResult> CreatePatient([FromBody]PatientCreateInput input)
        {
            if (dbContext.Patients.Count(p => p.OutpatientNo.Equals(input.OutpatientNo)) > 0)
            {
                return BadRequest(Json(new { Error = "患者已登记" }));
            }

            var patient = mapper.Map<Patient>(input);
            dbContext.Patients.Add(patient);
            await dbContext.SaveChangesAsync();

            return CreatedAtRoute("GetPatient", new { id = patient.Id }, mapper.Map<PatientOutput>(patient));
        }
        #endregion

        #region 修改患者信息
        /// <summary>
        /// 修改患者信息
        /// </summary>
        /// <param name="id"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPut("{id}")]
        [ValidateModel]
        [ProducesResponseType(typeof(void), 204)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(ValidationError), 422)]
        [ProducesResponseType(typeof(void), 500)]
        public async Task<IActionResult> UpdatePatient([FromRoute]int id, [FromBody]PatientUpdateInput input)
        {
            if (input.Id != id)
            {
                return BadRequest(Json(new { Error = "请求参数错误" }));
            }
            var patient = await dbContext.Patients.FirstOrDefaultAsync(p => p.Id == id);
            if (patient == null)
            {
                return NotFound(Json(new { Error = "该患者不存在" }));
            }

            dbContext.Entry(patient).CurrentValues.SetValues(input);
            await dbContext.SaveChangesAsync();

            return new NoContentResult();
        }
        #endregion

        #region 更新患者信息
        /// <summary>
        /// 更新患者信息
        /// </summary>
        /// <param name="id"></param>
        /// <param name="patchDoc"></param>
        /// <returns></returns>
        [HttpPatch("{id}")]
        [ProducesResponseType(typeof(PatientOutput), 201)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(ValidationError), 422)]
        [ProducesResponseType(typeof(void), 500)]
        public async Task<IActionResult> PatchPatientAsync([FromRoute]int id, [FromBody]JsonPatchDocument<PatientUpdateInput> patchDoc)
        {
            if (patchDoc == null)
            {
                return BadRequest(Json(new { Error = "请求参数错误" }));
            }

            var patient = await dbContext.Patients.FirstOrDefaultAsync(p => p.Id == id);
            if (patient == null)
            {
                return NotFound(Json(new { Error = "该患者不存在" }));
            }

            var input = mapper.Map<PatientUpdateInput>(patient);
            patchDoc.ApplyTo(input);

            TryValidateModel(input);
            if (!ModelState.IsValid)
            {
                return new ValidationFailedResult(ModelState);
            }

            dbContext.Entry(patient).CurrentValues.SetValues(input);
            await dbContext.SaveChangesAsync();

            return CreatedAtRoute("GetPatient", new { id = patient.Id }, mapper.Map<PatientOutput>(patient));
        }
        #endregion

        #region 删除患者
        /// <summary>
        /// 删除患者
        /// </summary>
        /// <param name="id">ID</param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(void), 204)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(void), 500)]
        public async Task<IActionResult> DeletePatient([FromRoute]int id)
        {
            var patient = await dbContext.Patients.FirstOrDefaultAsync(p => p.Id == id);
            if (patient == null)
            {
                return NotFound(Json(new { Error = "该患者不存在" }));
            }

            patient.Status = "删除";
            dbContext.Patients.Update(patient);
            await dbContext.SaveChangesAsync();

            return new NoContentResult();
        }

        /// <summary>
        /// 批量删除
        /// </summary>
        /// <param name="ids">ID数组</param>
        /// <returns></returns>
        [HttpDelete]
        [ProducesResponseType(typeof(void), 204)]
        [ProducesResponseType(typeof(void), 500)]
        public async Task<IActionResult> BatchDelete([FromBody]int[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                var patient = await dbContext.Patients.FirstOrDefaultAsync(p => p.Id == ids[i]);
                if (patient != null)
                {
                    patient.Status = "删除";
                    dbContext.Patients.Update(patient);
                }
            }

            await dbContext.SaveChangesAsync();

            return new NoContentResult();
        }
        #endregion

        #endregion

        #region 患者信息上报

        #region 病历上报
        /// <summary>
        /// 病历上报
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("UploadMedicalRecord")]
        [ValidateModel]
        [ProducesResponseType(typeof(UploadMedicalRecordOutput), 200)]
        [ProducesResponseType(typeof(ValidationError), 422)]
        [ProducesResponseType(typeof(void), 500)]
        public async Task<IActionResult> UploadMedicalRecord([FromBody]UploadMedicalRecordInput input)
        {
            Patient patient = await dbContext.Patients.FirstOrDefaultAsync(p => p.Id == input.PatientId);
            if (patient == null)
            {
                return BadRequest(Json(new { Error = "不存在该患者" }));
            }

            InitialDiagnosis initialDiagnosis = await dbContext.InitialDiagnoses.FirstOrDefaultAsync(i => i.PatientId == patient.Id);
            if (initialDiagnosis == null)
            {
                return BadRequest(Json(new { Error = "该患者未填写初步诊断" }));
            }

            PastMedicalHistory pastMedicalHistory = await dbContext.PastMedicalHistories.FirstOrDefaultAsync(p => p.PatientId == patient.Id);
            if (pastMedicalHistory == null)
            {
                return BadRequest(Json(new { Error = "该患者未填写既往史" }));
            }

            Symptom symptom = await dbContext.Symptoms.FirstOrDefaultAsync(s => s.PatientId == patient.Id);
            if (symptom == null)
            {
                return BadRequest(Json(new { Error = "该患者未填写症状体征信息" }));
            }

            List<FoodInfo> foodInfos = await dbContext.FoodInfos.Where(f => f.PatientId == patient.Id).ToListAsync();

            XmlHelper xmlHelper = new XmlHelper();

            string requestXml = "";
            if (input.OperationType == 3)
            {
                requestXml = xmlHelper.ConvertToXml(input.OperationType, apiOptions, patient);
            }
            else
            {
                requestXml = xmlHelper.ConvertToXml(input.OperationType, apiOptions, patient, initialDiagnosis, pastMedicalHistory, symptom, foodInfos);
            }

            ReportServiceClient reportService = new ReportServiceClient();
            string responseString = await reportService.WEBRequestAsync(Encrypt.Base64Encode(requestXml));
            string responseXmlString = Encrypt.Base64Decode(responseString);

            XmlReader xmlReader = XmlReader.Create(new StringReader(responseXmlString));
            XDocument xdoc = XDocument.Load(xmlReader);

            UploadMedicalRecordOutput output = new UploadMedicalRecordOutput
            {
                Code = Convert.ToInt32(xdoc.Element("接口").Element("操作状态").Value),
                Msg = xdoc.Element("接口").Element("状态描述").Value
            };

            if (input.OperationType == 1 && output.Code == 1)
            {
                patient.Status = "已上传";
            }

            if (input.OperationType == 3 && output.Code == 1)
            {
                patient.Status = "正常";
            }

            dbContext.Patients.Update(patient);
            dbContext.SaveChanges();

            return new ObjectResult(output);
        }
        #endregion

        #endregion
    }
}