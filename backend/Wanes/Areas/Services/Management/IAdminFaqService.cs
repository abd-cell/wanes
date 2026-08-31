using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminFaqService
{
    Task<BaseResponse<PageOutput<FaqRow>>> List(PageInput page, FaqCategory? category, bool? isPublished);
    Task<BaseResponse<FaqRow>> Get(int id);
    Task<BaseResponse<FaqRow>> Create(FaqInput input);
    Task<BaseResponse<FaqRow>> Update(int id, FaqInput input);
    Task<BaseResponse> Delete(int id);
}
