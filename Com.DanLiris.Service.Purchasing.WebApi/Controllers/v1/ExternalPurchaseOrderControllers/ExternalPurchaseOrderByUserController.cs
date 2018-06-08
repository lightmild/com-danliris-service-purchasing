﻿using AutoMapper;
using Com.DanLiris.Service.Purchasing.Lib.Facades.ExternalPurchaseOrderFacade;
using Com.DanLiris.Service.Purchasing.Lib.Models.ExternalPurchaseOrderModel;
using Com.DanLiris.Service.Purchasing.Lib.PDFTemplates;
using Com.DanLiris.Service.Purchasing.Lib.Services;
using Com.DanLiris.Service.Purchasing.Lib.ViewModels.ExternalPurchaseOrderViewModel;
using Com.DanLiris.Service.Purchasing.WebApi.Helpers;
using Com.Moonlay.NetCore.Lib.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Com.DanLiris.Service.Purchasing.WebApi.Controllers.v1.ExternalPurchaseOrderControllers
{
    [Produces("application/json")]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/external-purchase-orders/by-user")]
    [Authorize]
    public class ExternalPurchaseOrderByUserController : Controller
    {
        private string ApiVersion = "1.0.0";
        private readonly IMapper _mapper;
        private readonly ExternalPurchaseOrderFacade _facade;
        private readonly IdentityService identityService;

        public ExternalPurchaseOrderByUserController(IMapper mapper, ExternalPurchaseOrderFacade facade, IdentityService identityService)
        {
            _mapper = mapper;
            _facade = facade;
            this.identityService = identityService;
        }

        [HttpGet]
        public IActionResult Get(int page = 1, int size = 25, string order = "{}", string keyword = null, string filter = "{}")
        {
            identityService.Username = User.Claims.Single(p => p.Type.Equals("username")).Value;
            filter = filter.Replace("{", string.Empty).Replace("}", string.Empty);
            if (!filter.Equals(string.Empty)) filter += ", ";
            filter = string.Concat(filter, "'CreatedBy':'", identityService.Username, "'");
            filter = string.Concat("{", filter, "}");

            var Data = _facade.Read(page, size, order, keyword, filter);

            var newData = _mapper.Map<List<ExternalPurchaseOrderViewModel>>(Data.Item1);

            List<object> listData = new List<object>();
            listData.AddRange(
                newData.AsQueryable().Select(s => new
                {
                    s._id,
                    s.no,
                    s.orderDate,
                    s.supplier,
                    unit = new
                    {
                        division = new { s.unit.division.name },
                        s.unit.name
                    },
                    s.isPosted,
                    s.items
                }).ToList()
            );

            return Ok(new
            {
                apiVersion = ApiVersion,
                statusCode = General.OK_STATUS_CODE,
                message = General.OK_MESSAGE,
                data = listData,
                info = new Dictionary<string, object>
                {
                    { "count", listData.Count },
                    { "total", Data.Item2 },
                    { "order", Data.Item3 },
                    { "page", page },
                    { "size", size }
                },
            });
        }

        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            try
            {
                var indexAcceptPdf = Request.Headers["Accept"].ToList().IndexOf("application/pdf");

                ExternalPurchaseOrder model = _facade.ReadModelById(id);
                ExternalPurchaseOrderViewModel viewModel = _mapper.Map<ExternalPurchaseOrderViewModel>(model);

                if (indexAcceptPdf < 0)
                {
                    return Ok(new
                    {
                        apiVersion = ApiVersion,
                        statusCode = General.OK_STATUS_CODE,
                        message = General.OK_MESSAGE,
                        data = viewModel,
                    });
                }
                else
                {
                    ExternalPurchaseOrderPDFTemplate PdfTemplate = new ExternalPurchaseOrderPDFTemplate();
                    MemoryStream stream = PdfTemplate.GeneratePdfTemplate(viewModel);

                    return new FileStreamResult(stream, "application/pdf")
                    {
                        FileDownloadName = $"{viewModel.no}.pdf"
                    };
                }
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody]ExternalPurchaseOrderViewModel vm)
        {
            identityService.Token = Request.Headers["Authorization"].First().Replace("Bearer ", "");
            identityService.Username = User.Claims.Single(p => p.Type.Equals("username")).Value;

            ExternalPurchaseOrder m = _mapper.Map<ExternalPurchaseOrder>(vm);

            ValidateService validateService = (ValidateService)_facade.serviceProvider.GetService(typeof(ValidateService));

            try
            {
                validateService.Validate(vm);

                int result = await _facade.Create(m, identityService.Username);

                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.CREATED_STATUS_CODE, General.OK_MESSAGE)
                    .Ok();
                return Created(String.Concat(Request.Path, "/", 0), Result);
            }
            catch (ServiceValidationExeption e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.BAD_REQUEST_STATUS_CODE, General.BAD_REQUEST_MESSAGE)
                    .Fail(e);
                return BadRequest(Result);

            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }

        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put([FromRoute]int id, [FromBody]ExternalPurchaseOrderViewModel vm)
        {
            identityService.Username = User.Claims.Single(p => p.Type.Equals("username")).Value;

            ExternalPurchaseOrder m = _mapper.Map<ExternalPurchaseOrder>(vm);

            ValidateService validateService = (ValidateService)_facade.serviceProvider.GetService(typeof(ValidateService));

            try
            {
                validateService.Validate(vm);

                int result = await _facade.Update(id, m, identityService.Username);

                return NoContent();
            }
            catch (ServiceValidationExeption e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.BAD_REQUEST_STATUS_CODE, General.BAD_REQUEST_MESSAGE)
                    .Fail(e);
                return BadRequest(Result);

            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }

        }

        [HttpDelete("{id}")]
        public IActionResult Delete([FromRoute]int id)
        {
            identityService.Username = User.Claims.Single(p => p.Type.Equals("username")).Value;

            try
            {
                _facade.Delete(id, identityService.Username);

                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE);
            }
        }

        [HttpPost("post")]
        public IActionResult EPOPost([FromBody]List<ExternalPurchaseOrderViewModel> ListExternalPurchaseOrderViewModel)
        {
            identityService.Username = User.Claims.Single(p => p.Type.Equals("username")).Value;
            try
            {
                _facade.EPOPost(
                    ListExternalPurchaseOrderViewModel.Select(vm => _mapper.Map<ExternalPurchaseOrder>(vm)).ToList(), identityService.Username
                );

                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE);
            }
        }

        [HttpPut("unpost/{id}")]
        public IActionResult EPOUnpost([FromRoute]int id)
        {
            identityService.Username = User.Claims.Single(p => p.Type.Equals("username")).Value;

            try
            {
                _facade.EPOUnpost(id, identityService.Username);

                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE);
            }
        }

        //[HttpPut("cancel/{id}")]
        //public IActionResult EPOCancel([FromRoute]int id)
        //{
        //    identityService.Username = User.Claims.Single(p => p.Type.Equals("username")).Value;

        //    try
        //    {
        //        _facade.EPOCancel(id, identityService.Username);

        //        return NoContent();
        //    }
        //    catch (Exception)
        //    {
        //        return StatusCode(General.INTERNAL_ERROR_STATUS_CODE);
        //    }
        //}

        //[HttpPut("close/{id}")]
        //public IActionResult EPOClose([FromRoute]int id)
        //{
        //    identityService.Username = User.Claims.Single(p => p.Type.Equals("username")).Value;

        //    try
        //    {
        //        _facade.EPOClose(id, identityService.Username);

        //        return NoContent();
        //    }
        //    catch (Exception)
        //    {
        //        return StatusCode(General.INTERNAL_ERROR_STATUS_CODE);
        //    }
        //}
    }
}
